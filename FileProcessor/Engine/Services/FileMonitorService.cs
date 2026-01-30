using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;

namespace FileProcessor.Engine.Services;

/// <summary>
/// 负责监控文件夹变化并协调处理流程的服务
/// </summary>
public class FileMonitorService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly FileOrchestrator _orchestrator;
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();

    // 定义事件：当快照生成时触发
    public event Action<FileSnapshot>? OnSnapshotCreated;

    // 参数配置
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 500;
    private const int DebounceDelayMs = 1000;

    public FileMonitorService(string watchPath, FileOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;

        if (!Directory.Exists(watchPath)) Directory.CreateDirectory(watchPath);

        _watcher = new FileSystemWatcher(watchPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _debouncers.AddOrUpdate(e.FullPath,
            path => new Timer(_ => ProcessFileWithRetry(path), null, DebounceDelayMs, Timeout.Infinite),
            (path, oldTimer) =>
            {
                oldTimer.Change(DebounceDelayMs, Timeout.Infinite);
                return oldTimer;
            });
    }

    private void ProcessFileWithRetry(string filePath)
    {
        int retries = 0;
        while (retries < MaxRetries)
        {
            try
            {
                // 确保文件没有被写入进程锁定
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var snapshot = _orchestrator.ProcessFile(filePath);

                    if (snapshot != null)
                    {
                        Console.WriteLine($"[Success] 文件 {Path.GetFileName(filePath)} 解析成功");

                        // 【核心改动】触发事件，通知所有订阅者（比如 SnapshotManager）
                        OnSnapshotCreated?.Invoke(snapshot);
                    }
                }
                break;
            }
            catch (IOException)
            {
                retries++;
                Thread.Sleep(RetryDelayMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] 处理 {filePath} 异常: {ex.Message}");
                break;
            }
        }

        if (_debouncers.TryRemove(filePath, out var timer)) timer.Dispose();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var timer in _debouncers.Values) timer.Dispose();
    }
}