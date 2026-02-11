using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 增强型项目监控服务：
    /// 实现基于 Hash 的智能去重与哨兵信号处理。
    /// 支持自动识别批次生命周期。
    /// </summary>
    public class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;

        // Hash 缓存：FilePath -> LastHash
        // 用于在监控层直接拦截内容未变的文件，避免无效解析
        private readonly ConcurrentDictionary<string, string> _fileHashCache = new();
        
        private FileSystemWatcher? _watcher;

        public ProjectMonitorService(FileOrchestrator orchestrator, IVersionCoordinator versionCoordinator)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
        }

        /// <summary>
        /// 启动对目标根目录的深度监控
        /// </summary>
        /// <param name="path">监控根路径（包含“设计结果”子目录）</param>
        public void StartScanning(string path)
        {
            if (!Directory.Exists(path)) return;

            // 1. 初始扫描：建立基准指纹 (异步执行，避免阻塞启动)
            Task.Run(() => InitialScan(path));

            // 2. 启动实时监控
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
                Filter = "*.out", // 仅锁定 YJK 生成的 .out 文件                
            };

            _watcher.Changed += (s, e) => HandleEvent(e.FullPath);
            _watcher.Created += (s, e) => HandleEvent(e.FullPath);
            // Renamed 需要特殊处理，这里简化处理，视为新文件创建
            _watcher.Renamed += (s, e) => HandleEvent(e.FullPath);
            _watcher.EnableRaisingEvents = true;

            Console.WriteLine($"[Monitor] 智能监控已就绪: {path}");
        }

        /// <summary>
        /// 初始扫描逻辑：遍历目录内所有已存在的 .out 文件
        /// </summary>
        /// <param name="rootPath"></param>
        private void InitialScan(string rootPath)
        {
            // 扫描所有潜在的目标文件（这里简单以 .out 举例，可根据实际需求调整通配符）
            try
            {
                var files = Directory.GetFiles(rootPath, "*.out", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    HandleEvent(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] 初始扫描失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 核心处理逻辑：负责 Hash 过滤、哨兵识别、任务下发
        /// </summary>
        private void HandleEvent(string fullPath)
        {
            // 基础准入：确保文件存在
            if (!File.Exists(fullPath)) return;

            try
            {
                string fileName = Path.GetFileName(fullPath).ToLower();
                string dirName = Path.GetDirectoryName(fullPath) ?? "";

                // --- A. 哨兵信号处理 (保持不变) ---
                // 信号处理逻辑：检查“设计结果”目录下的哨兵文件
                if (dirName.EndsWith("设计结果", StringComparison.OrdinalIgnoreCase))
                {
                    if (fileName == "check.out")
                    {
                        _versionCoordinator.StartNewBatch(fileName);
                        return; // 哨兵文件本身不含业务数据，不解析
                    }

                    if (fileName == "mainjss.out")
                    {
                        _versionCoordinator.CommitCurrentBatch(fileName);
                        return; // 哨兵文件不解析
                    }
                }

                // --- B. 智能准入控制 (Hash Check) ---

                // 1. 计算当前物理文件的 Hash
                string currentHash = CalculateFileHash(fullPath);

                // 2. 对比缓存：如果 Hash 没变，直接忽略本次事件
                // (注意：如果是 InitialScan，缓存中没有，TryGetValue 返回 false，会继续执行)
                if (_fileHashCache.TryGetValue(fullPath, out string? lastHash) && lastHash == currentHash)
                {
                    // 文件内容未实质变更，跳过
                    return;
                }

                // 3. 更新缓存
                _fileHashCache[fullPath] = currentHash; //测试注释

                // --- C. 构造任务并下发 ---

                // 2. 业务数据流水线：无论是否在批次内，只要是关心的文件就进行解析
                // context 会携带当时的 VersionId（可能是 Live，也可能是正在运行的 Batch_xxx）
                var context = new ProcessingTaskContext
                {
                    FilePath = fullPath,
                    FileHash = currentHash, // 将计算好的指纹注入上下文
                    BoundVersionId = _versionCoordinator.CurrentVersionId,
                    TriggerTime = DateTime.Now
                };

                // 只有通过了 Hash 检查的任务才会进入编排器
                // 将决策权交给 Orchestrator：
                // 如果 isRecording 为 true，Orchestrator 会将其存入 _pendingTasks 字典实现“多变一”
                // 如果 isRecording 为 false，Orchestrator 会立即执行并生成时间戳版本记录历史
                _orchestrator.EnqueueTask(context, _versionCoordinator.IsRecording);
            }
            catch (IOException)
            {
                // 文件正被 YJK 写入占用中，忽略本次触发，等待下一次（通常写入完成会再次触发）
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] 处理文件 {Path.GetFileName(fullPath)} 时异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算文件的 SHA256 哈希值
        /// </summary>
        private string CalculateFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            // 使用 FileShare.ReadWrite 避免与 YJK 抢占文件锁
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hashBytes = sha.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }
}