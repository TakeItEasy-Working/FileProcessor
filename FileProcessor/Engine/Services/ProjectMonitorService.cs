using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Services
{
    /// <summary>
    /// 引擎状态枚举：控制内核的行为模式
    /// </summary>
    public enum EngineState
    {
        Idle,       // 停机
        Scanning,   // 初始扫描阶段
        Monitoring  // 实时监控阶段
    }

    /// <summary>
    /// 项目监控服务：回滚并修复版
    /// 仅负责监听文件系统变动，不直接持有 Template 集合，通过 Orchestrator 驱动。
    /// </summary>
    public partial class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly ISnapshotManager _snapshotManager;

        private FileSystemWatcher? _sentinelWatcher;
        private FileSystemWatcher? _resultWatcher;

        private EngineState _currentState = EngineState.Idle;
        private string _currentRoot = string.Empty;
        private string _sentinelName = "dsnctrl.ini";

        public event Action<EngineState>? StateChanged;

        /// <summary>
        /// 构造函数：回滚至 3 参数版本，确保与初始 DI 容器配置兼容
        /// </summary>
        public ProjectMonitorService(
            FileOrchestrator orchestrator,
            IVersionCoordinator versionCoordinator,
            ISnapshotManager snapshotManager)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
            _snapshotManager = snapshotManager;
        }

        /// <summary>
        /// 启动监控任务（回滚版本）
        /// </summary>
        public async Task StartMonitoringAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            StopMonitoring();
            _currentRoot = path;

            UpdateState(EngineState.Scanning);

            // 1. 清理仓库旧数据
            _snapshotManager.Clear();

            // 2. 执行初始全量扫描
            await Task.Run(() => PerformInitialScan(path));

            // 3. 开启实时监听
            SetupWatchers(path);

            UpdateState(EngineState.Monitoring);
        }

        public void StopMonitoring()
        {
            _sentinelWatcher?.Dispose();
            _resultWatcher?.Dispose();
            _sentinelWatcher = null;
            _resultWatcher = null;
            UpdateState(EngineState.Idle);
        }

        /// <summary>
        /// 初始扫描逻辑：修复了 CS1061 错误。
        /// 不再调用 Orchestrator 不存在的 ScanDirectory，而是利用其暴露的 Templates 进行扫描。
        /// </summary>
        private void PerformInitialScan(string rootPath)
        {
            // 锁定版本为初始版本
            _versionCoordinator.ForceVersion("Initial_History");

            // 获取编排器中注册的所有模板
            var templates = _orchestrator.Templates;

            if (templates != null)
            {
                foreach (var template in templates)
                {
                    string targetDir = string.IsNullOrEmpty(template.SubDirectory)
                        ? rootPath
                        : Path.Combine(rootPath, template.SubDirectory);

                    if (!Directory.Exists(targetDir)) continue;

                    var files = Directory.GetFiles(targetDir, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        if (Regex.IsMatch(Path.GetFileName(file), template.FileNamePattern))
                        {
                            // 调用编排器已有的 ProcessFile 方法
                            _orchestrator.ProcessFile(file, "Initial_History", template);
                        }
                    }
                }
            }

            _snapshotManager.NotifyBatchComplete();
        }

        private void SetupWatchers(string path)
        {
            // 监听哨兵文件
            _sentinelWatcher = new FileSystemWatcher(path, _sentinelName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _sentinelWatcher.Changed += OnSentinelChanged;

            // 监听结果文件（递归监听所有子目录）
            _resultWatcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _resultWatcher.Changed += OnFileChanged;
            _resultWatcher.Created += OnFileChanged;
        }

        private void OnSentinelChanged(object sender, FileSystemEventArgs e)
        {
            if (_currentState != EngineState.Monitoring) return;
            _versionCoordinator.TriggerNewVersion();
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_currentState != EngineState.Monitoring) return;

            // 获取当前版本并解析
            string currentVersion = _versionCoordinator.GetCurrentVersion();
            _orchestrator.ProcessFile(e.FullPath, currentVersion);
        }

        private void UpdateState(EngineState newState)
        {
            _currentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}