using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using FileProcessor.DebugHelpers;
using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 增强型项目监控服务：文件夹全局防抖版 (Storm & Silence)
    /// 无论文件生成顺序如何错乱，只以“文件夹彻底静默 2.5 秒”作为唯一安全收网的判定标准。
    /// </summary>
    public class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly ConcurrentDictionary<string, string> _fileHashCache = new();
        private FileSystemWatcher? _watcher;
        private string _monitorRootPath = string.Empty;

        // ==========================================
        // 核心状态机设计
        // ==========================================
        private enum MonitorState { Idle, Calculating, Sweeping }
        private MonitorState _currentState = MonitorState.Idle;

        // 全局文件夹防抖计时器
        private CancellationTokenSource? _folderDebounceCts;
        private readonly object _stateLock = new object();

        public ProjectMonitorService(FileOrchestrator orchestrator, IVersionCoordinator versionCoordinator)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
        }

        public void StartScanning(string path)
        {
            _monitorRootPath = path;
            if (!Directory.Exists(path)) return;

            try
            {
                _fileHashCache.Clear();
                _orchestrator.BeginInitialization();

                var files = Directory.GetFiles(path, "*.out", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string currentHash = CalculateFileHash(file);
                    _fileHashCache[file] = currentHash;
                    _orchestrator.ProcessInitialFile(file, currentHash);
                }

                _orchestrator.EndInitialization();
                SetupWatcher(path);
            }
            catch (Exception ex)
            {
                Log.Debug($"[ProjectMonitorService] 项目启动失败: {ex.Message}");
                _orchestrator.EndInitialization();
            }
            Log.Debug($"[ProjectMonitorService] 全局防抖监控已就绪: {path}");
        }

        private void SetupWatcher(string path)
        {
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.out"
            };

            // 监听所有变动、创建、重命名、删除
            _watcher.Changed += (s, e) => HandleEvent(e.FullPath);
            _watcher.Created += (s, e) => HandleEvent(e.FullPath);
            _watcher.Deleted += (s, e) => HandleEvent(e.FullPath);
            _watcher.Renamed += (s, e) => HandleEvent(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }

        private void HandleEvent(string fullPath)
        {
            string fileName = Path.GetFileName(fullPath).ToLower();
            string dirName = Path.GetDirectoryName(fullPath) ?? "";

            // 只监控设计结果目录内的文件
            if (!dirName.EndsWith("设计结果", StringComparison.OrdinalIgnoreCase)) return;

            lock (_stateLock)
            {
                // 1. 发现开门哨兵，进入雷暴静默期
                if (fileName == "check.out" && _currentState == MonitorState.Idle)
                {
                    _currentState = MonitorState.Calculating;
                    _versionCoordinator.StartNewBatch(fileName);
                    return;
                }

                // 2. 发现结束信号的影子（不管它是不是最后一个）
                // 只要出现了，就说明 YJK 已经算到了最后阶段，进入扫荡准备期
                if (_currentState == MonitorState.Calculating &&
                   (fileName == "warnning.out" || fileName == "wdcnl.out" || fileName == "wbrc.out" || fileName == "wmass.out"))
                {
                    _currentState = MonitorState.Sweeping;
                }

                // 3. 文件夹全局防抖倒计时 (核心机制)
                // 只有处于 Idle(Live散件模式) 或 Sweeping(等待彻底结束) 时，文件变动才会触发倒计时
                if (_currentState == MonitorState.Sweeping || _currentState == MonitorState.Idle)
                {
                    // 批次收尾需要静默 2.5 秒；散件修改只需静默 1 秒
                    int delayMs = _currentState == MonitorState.Sweeping ? 2500 : 1000;

                    // 只要有任何文件变动，无情打断之前的倒计时，重新开始算！
                    _folderDebounceCts?.Cancel();
                    _folderDebounceCts?.Dispose();
                    _folderDebounceCts = new CancellationTokenSource();

                    // 捕获触发时的状态
                    MonitorState stateAtTrigger = _currentState;

                    Task.Delay(delayMs, _folderDebounceCts.Token).ContinueWith(t =>
                    {
                        // 如果 2.5 秒内整个文件夹都没有任何文件变动，倒计时成功归零，执行扫荡！
                        if (!t.IsCanceled)
                        {
                            ExecuteFolderSweep(stateAtTrigger);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 文件夹彻底安静后的全量安全扫荡
        /// </summary>
        private void ExecuteFolderSweep(MonitorState triggerState)
        {
            lock (_stateLock)
            {
                // 极低概率防抖：如果扫荡前又被拉回计算状态则取消
                if (_currentState == MonitorState.Calculating) return;

                Log.Debug($"[ProjectMonitorService] 文件夹彻底静默，模式: {triggerState}，开始全量安全入库...");

                string versionId = triggerState == MonitorState.Sweeping
                    ? _versionCoordinator.CurrentVersionId
                    : _versionCoordinator.GenerateLiveVersionId();

                bool hasDataChanges = false;
                ProcessingTaskContext? pendingTask = null; // 用于实现“延迟一拍下发”

                // 物理扫描整个文件夹，一个都不漏！
                var files = Directory.GetFiles(_monitorRootPath, "*.out", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string fn = Path.GetFileName(file).ToLower();
                    if (fn == "check.out" || fn == "mainjss.out" || fn == "warnning.out" || fn == "wdcnl.out" || fn == "wbrc.out") continue;

                    try
                    {
                        string currentHash = CalculateFileHash(file);
                        if (_fileHashCache.TryGetValue(file, out string? lastHash) && lastHash == currentHash)
                        {
                            continue; // 没变动的文件忽略
                        }
                        _fileHashCache[file] = currentHash;
                        hasDataChanges = true;

                        var context = new ProcessingTaskContext
                        {
                            FilePath = file,
                            FileHash = currentHash,
                            BoundVersionId = versionId, // 这里稳稳地绑住了 Batch_xxx
                            TriggerTime = DateTime.Now,
                            IsFinalCommit = false       // 默认不开枪
                        };

                        // 延迟一拍下发：为了把 IsFinalCommit 标记留在遍历的“最后一个文件”上
                        if (pendingTask != null)
                        {
                            // 传回正确的 isRecording 状态
                            _orchestrator.EnqueueTask(pendingTask, triggerState == MonitorState.Sweeping);
                        }
                        pendingTask = context;
                    }
                    catch (IOException) { /* 忽略极罕见冲突 */ }
                    catch (Exception ex)
                    {
                        Log.Debug($"[ProjectMonitorService] 解析 {fn} 异常: {ex.Message}");
                    }
                }

                // 处理遍历拿到的最后一个文件（如果是扫荡模式，给它戴上发令枪皇冠）
                if (pendingTask != null)
                {
                    if (triggerState == MonitorState.Sweeping)
                    {
                        // 扣动扳机！触发 Orchestrator 清空它的暂存队列并解析
                        pendingTask = pendingTask with { IsFinalCommit = true };
                    }
                    _orchestrator.EnqueueTask(pendingTask, triggerState == MonitorState.Sweeping);
                }

                // 给系统 0.5 秒钟的时间让数据彻底落库
                Task.Delay(500).ContinueWith(_ =>
                {
                    // 注意：无论有没有数据变化，只要是批次扫荡，必须关门！
                    // 否则系统会永远卡在 Recording 状态，导致后续全是 Bug
                    if (triggerState == MonitorState.Sweeping)
                    {
                        _versionCoordinator.CommitCurrentBatch("Folder_Sweep_Done");
                    }
                    // 如果是散件 Live 模式，只有真发生了数据变化，才去打扰 UI 刷新
                    else if (hasDataChanges)
                    {
                        _versionCoordinator.Commit(versionId);
                    }
                });

                // 扫荡结束，恢复空闲态，迎接下一次操作
                _currentState = MonitorState.Idle;
            }
        }

        private string CalculateFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hashBytes = sha.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }
}