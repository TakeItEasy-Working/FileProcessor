using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;

namespace FileProcessor.Engine.Services;

/// <summary>
/// 增强版项目监控服务：支持双路监控（哨兵+数据）、版本协调、防抖及稳定性检测
/// </summary>
public class ProjectMonitorService : IDisposable
{
    private readonly FileOrchestrator _orchestrator;
    private readonly IVersionCoordinator _versionCoordinator;
    private readonly IEnumerable<IFileTemplate> _templates;

    // 监控器集合：Key 为路径，Value 为观察者
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();

    private const string SentinelFile = "dsnctrl.ini";
    private const int DebounceDelayMs = 1000;
    private const int MaxStabilityChecks = 10;
    private const int StabilityCheckDelayMs = 300;

    public ProjectMonitorService(
        FileOrchestrator orchestrator,
        IVersionCoordinator coordinator,
        IEnumerable<IFileTemplate> templates)
    {
        _orchestrator = orchestrator;
        _versionCoordinator = coordinator;
        _templates = templates;
    }

    public void Start(string rootPath)
    {
        Stop(); // 清理旧连接

        if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

        // 1. 开启根目录监控 (针对 dsnctrl.ini 哨兵)
        CreateWatcher(rootPath, SentinelFile, OnSentinelEvent);

        // 2. 开启子目录监控 (针对各插件定义的 SubDirectory)
        var subDirs = _templates.Select(t => t.SubDirectory).Distinct();
        foreach (var subDir in subDirs)
        {
            string targetPath = Path.Combine(rootPath, subDir);
            if (!Directory.Exists(targetPath)) continue;

            // 监控子目录下所有文件变动
            CreateWatcher(targetPath, "*.*", OnDataFileEvent);

            // 启动时扫描：将已有文件放入防抖队列处理
            InitialScan(targetPath);
        }
    }

    private void CreateWatcher(string path, string filter, FileSystemEventHandler handler)
    {
        var watcher = new FileSystemWatcher(path, filter)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
            InternalBufferSize = 64 * 1024
        };

        watcher.Created += handler;
        watcher.Changed += handler;

        // 修复 CS0029 错误：
        // 使用 lambda 表达式将 RenamedEventArgs 适配给 FileSystemEventHandler 所需的参数
        watcher.Renamed += (s, e) => handler(s, e);

        _watchers.TryAdd(path + filter, watcher);
        Console.WriteLine($"[Watcher] 开始监控: {path} (Filter: {filter})");
    }

    private void OnSentinelEvent(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"[Sentinel] 哨兵文件触发: {e.Name}");
        // 关键点：通知协调器，开启/更新版本窗口
        _versionCoordinator.NotifySentinelChanged(e.Name ?? SentinelFile);
    }

    private void OnDataFileEvent(object sender, FileSystemEventArgs e)
    {
        // 过滤哨兵文件本身（防止逻辑循环）
        if (e.Name == SentinelFile) return;

        // 防抖处理
        _debouncers.AddOrUpdate(e.FullPath,
            path => new Timer(_ => ProcessFileWorkflow(path), null, DebounceDelayMs, Timeout.Infinite),
            (path, oldTimer) =>
            {
                oldTimer.Change(DebounceDelayMs, Timeout.Infinite);
                return oldTimer;
            });
    }

    private void ProcessFileWorkflow(string filePath)
    {
        try
        {
            // 1. 稳定性检查
            if (!WaitForFileStable(filePath)) return;

            // 2. 此时调用重构后的 Orchestrator
            // 核心改进：从 versionCoordinator 获取当前的 VersionId
            string currentVid = _versionCoordinator.CurrentVersionId;

            Console.WriteLine($"[Processing] 开始解析: {Path.GetFileName(filePath)} (Version: {currentVid})");

            // 解决了你的报错：现在提供了 versionId 参数
            _orchestrator.ProcessFile(filePath, currentVid);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] 处理文件失败 {filePath}: {ex.Message}");
        }
        finally
        {
            if (_debouncers.TryRemove(filePath, out var timer)) timer.Dispose();
        }
    }

    // 复用你之前的稳定检测逻辑
    private bool WaitForFileStable(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        long lastSize = -1;
        DateTime lastWrite = DateTime.MinValue;

        for (int i = 0; i < MaxStabilityChecks; i++)
        {
            var fi = new FileInfo(filePath);
            if (fi.Length == lastSize && fi.LastWriteTimeUtc == lastWrite) return true;
            lastSize = fi.Length;
            lastWrite = fi.LastWriteTimeUtc;
            Thread.Sleep(StabilityCheckDelayMs);
        }
        return false;
    }

    private void InitialScan(string path)
    {
        foreach (var file in Directory.GetFiles(path))
        {
            ProcessFileWorkflow(file);
        }
    }

    public void Stop()
    {
        foreach (var w in _watchers.Values) w.Dispose();
        _watchers.Clear();
        foreach (var t in _debouncers.Values) t.Dispose();
        _debouncers.Clear();
    }

    public void Dispose() => Stop();
}