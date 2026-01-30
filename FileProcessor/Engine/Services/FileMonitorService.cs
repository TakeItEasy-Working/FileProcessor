using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;

namespace FileProcessor.Engine.Services;

/// <summary>
/// 负责监控文件夹变化并协调处理流程的服务（增强版：更详细日志、支持重命名事件、等待文件稳定、启动时扫描已有文件）
/// </summary>
public class FileMonitorService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly FileOrchestrator _orchestrator;
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();

    // 定义事件：当快照生成时触发
    public event Action<FileSnapshot>? OnSnapshotCreated;

    // 参数配置
    private const int MaxStabilityChecks = 10;      // 最多检查多少次文件稳定性
    private const int StabilityCheckDelayMs = 300;  // 每次检查间隔
    private const int DebounceDelayMs = 1000;       // 防抖延迟（Timer）
    private const int OverallRetryDelayMs = 500;    // 处理失败时的重试间隔（保留用于回退）

    public FileMonitorService(string watchPath, FileOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;

        if (!Directory.Exists(watchPath)) Directory.CreateDirectory(watchPath);

        _watcher = new FileSystemWatcher(watchPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            // 可按需限制过滤器，例如只监控 .out 文件：Filter = "*.out"
            // Filter = "*.out",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.Renamed += (s, e) => OnFileEvent(s, e); // 处理移动/重命名进来的文件
        _watcher.Error += (s, e) => Console.WriteLine($"[Watcher Error] {e.GetException()?.Message}");

        // 增强初始化日志
        Console.WriteLine($"[Watcher INIT] path={watchPath}");
        Console.WriteLine($"[Watcher INIT] EnableRaisingEvents={_watcher.EnableRaisingEvents}, Filter={_watcher.Filter ?? "*"}, IncludeSubdirs={_watcher.IncludeSubdirectories}");
        _watcher.InternalBufferSize = 64 * 1024; // 增大缓冲，减少丢事件风险

        // 启动时对已有文件做初次扫描（统一使用 debouncer 入队，避免流程分叉）
        InitialScan(watchPath);
    }

    private void InitialScan(string watchPath)
    {
        try
        {
            if (!Directory.Exists(watchPath))
            {
                Console.WriteLine($"[Watcher INIT] 目录不存在，跳过初次扫描: {watchPath}");
                return;
            }

            var files = Directory.GetFiles(watchPath);
            Console.WriteLine($"[Watcher INIT] 初次扫描发现 {files.Length} 个文件，已入队处理。");

            foreach (var file in files)
            {
                // 与 OnFileEvent 使用相同的 debouncer 行为
                _debouncers.AddOrUpdate(file,
                    path => new Timer(_ => ProcessFileWithStabilityCheck(path), null, DebounceDelayMs, Timeout.Infinite),
                    (path, oldTimer) =>
                    {
                        oldTimer.Change(DebounceDelayMs, Timeout.Infinite);
                        return oldTimer;
                    });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Watcher INIT] 初次扫描失败: {ex.Message}");
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        try
        {
            Console.WriteLine($"[Watcher] 事件: {e.ChangeType} -> {e.FullPath}");
            // 防抖：同一路径短时内只安排一次处理
            _debouncers.AddOrUpdate(e.FullPath,
                path => new Timer(_ => ProcessFileWithStabilityCheck(path), null, DebounceDelayMs, Timeout.Infinite),
                (path, oldTimer) =>
                {
                    oldTimer.Change(DebounceDelayMs, Timeout.Infinite);
                    return oldTimer;
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Watcher Error] 处理事件失败: {ex.Message}");
        }
    }

    private void ProcessFileWithStabilityCheck(string filePath)
    {
        try
        {
            // 等待文件达到“稳定”状态：文件存在且 size/lastWrite 在两次短间隔检测中相同
            if (!WaitForFileStable(filePath))
            {
                Console.WriteLine($"[Error] 文件未能在规定时间内稳定: {filePath}");
                return;
            }

            // 此时认为文件可以安全读取（其他进程可能已释放写句柄）
            // 使用 FileShare.Read 来避免与写入句柄冲突（我们已通过稳定检测确定内容应该完整）
            FileSnapshot? snapshot = null;
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 这里打开文件保证可读性，实际解析由 orchestrator 完成（它内部会用 StreamReader）
                    snapshot = _orchestrator.ProcessFile(filePath);
                }
            }
            catch (IOException ioEx)
            {
                // 作为回退，尝试再等一小段时间再重试一次
                Console.WriteLine($"[IO] 读取文件时出现 IO 异常，稍候重试: {ioEx.Message}");
                Thread.Sleep(OverallRetryDelayMs);
                snapshot = _orchestrator.ProcessFile(filePath); // 最后一次尝试
            }

            if (snapshot != null)
            {
                Console.WriteLine($"[Success] 文件 {Path.GetFileName(filePath)} 解析成功");
                OnSnapshotCreated?.Invoke(snapshot);
            }
            else
            {
                Console.WriteLine($"[Info] 文件 {Path.GetFileName(filePath)} 未被解析（可能模板不匹配）");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] 处理 {filePath} 异常: {ex.Message}");
        }
        finally
        {
            if (_debouncers.TryRemove(filePath, out var timer)) timer.Dispose();
        }
    }

    private bool WaitForFileStable(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Debug] 文件不存在: {filePath}");
                return false;
            }

            long lastSize = -1;
            DateTime lastWrite = DateTime.MinValue;

            for (int i = 0; i < MaxStabilityChecks; i++)
            {
                var fi = new FileInfo(filePath);
                long size = fi.Length;
                DateTime write = fi.LastWriteTimeUtc;

                if (size == lastSize && write == lastWrite)
                {
                    //// 两次检测一致 -> 稳定
                    //Console.WriteLine($"[Debug] 文件稳定: {filePath} (size={size})");
                    return true;
                }

                lastSize = size;
                lastWrite = write;

                Thread.Sleep(StabilityCheckDelayMs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] 检查文件稳定性失败: {ex.Message}");
        }
        return false;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var timer in _debouncers.Values) timer.Dispose();
    }
}