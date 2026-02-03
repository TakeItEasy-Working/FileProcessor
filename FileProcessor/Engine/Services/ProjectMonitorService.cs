using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Services
{
    /// <summary>
    /// 引擎状态枚举：控制内核的行为模式
    /// </summary>
    public enum EngineState
    {
        Idle,       // 停机
        Scanning,   // 初始扫描阶段（处理历史存量）
        Monitoring  // 实时监控阶段（处理实时增量）
    }

    public partial class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;

        private FileSystemWatcher _sentinelWatcher; // 哨兵监听（dsnctrl.ini）
        private FileSystemWatcher _resultWatcher;   // 结果文件监听（子目录）

        private EngineState _currentState = EngineState.Idle;
        private string _currentRoot = string.Empty;

        // 向外暴露状态，UI 可以据此显示“正在初始化...”
        public event Action<EngineState> StateChanged;

        public ProjectMonitorService(
            FileOrchestrator orchestrator,
            IVersionCoordinator versionCoordinator,
            ISnapshotManager snapshotManager,
            IEnumerable<IFileTemplate> templates)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
            _snapshotManager = snapshotManager;
            _templates = templates;

            InitializeWatchers();
        }

        private void InitializeWatchers()
        {
            // 1. 初始化哨兵监听器（监听根目录的 .ini）
            _sentinelWatcher = new FileSystemWatcher();
            _sentinelWatcher.Filter = "*.ini";
            _sentinelWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
            _sentinelWatcher.Changed += OnSentinelChanged;
            _sentinelWatcher.Created += OnSentinelChanged;

            // 2. 初始化结果监听器（监听所有子目录的变动）
            _resultWatcher = new FileSystemWatcher();
            _resultWatcher.IncludeSubdirectories = true;
            _resultWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _resultWatcher.Changed += OnFileChanged;
            _resultWatcher.Created += OnFileChanged;
        }

        /// <summary>
        /// 启动引擎：扫描 -> 监听
        /// </summary>
        public void Start(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;

            // 如果路径更换，先停止旧任务
            if (_currentState != EngineState.Idle) Stop();

            _currentRoot = projectPath;

            // --- 步骤 1: 进入扫描状态 ---
            UpdateState(EngineState.Scanning);

            // 清理旧仓库数据，准备新扫描
            _snapshotManager.Clear();

            // 执行初始扫描
            PerformInitialScan(projectPath);

            // --- 步骤 2: 切换到监控状态 ---
            UpdateState(EngineState.Monitoring);

            _sentinelWatcher.Path = projectPath;
            _sentinelWatcher.EnableRaisingEvents = true;

            _resultWatcher.Path = projectPath;
            _resultWatcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _sentinelWatcher.EnableRaisingEvents = false;
            _resultWatcher.EnableRaisingEvents = false;
            UpdateState(EngineState.Idle);
        }

        /// <summary>
        /// 核心逻辑：扫描已有文件
        /// </summary>
        private void PerformInitialScan(string rootPath)
        {
            // 设定一个特殊的版本号用于标记存量数据
            string initialVersion = $"Initial_Scan_{DateTime.Now:yyyyMMdd_HHmm}";

            // 强制版本协调器锁定在此版本，不触发正常的 3s 倒计时
            _versionCoordinator.ForceVersion(initialVersion);

            foreach (var template in _templates)
            {
                // 确定搜索目录
                string searchDir = string.IsNullOrEmpty(template.SubDirectory)
                    ? rootPath
                    : Path.Combine(rootPath, template.SubDirectory);

                if (!Directory.Exists(searchDir)) continue;

                // 寻找符合正则的文件
                var files = Directory.GetFiles(searchDir, "*.*")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f), template.FileNamePattern));

                foreach (var file in files)
                {
                    try
                    {
                        // 执行解析并直接存入仓库
                         _orchestrator.ProcessFile(file, initialVersion, template);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志，但不中断扫描
                        System.Diagnostics.Debug.WriteLine($"扫描文件失败 {file}: {ex.Message}");
                    }
                }
            }

            // 扫描结束，发出通知（让 UI 一次性刷新）
            // 注意：需在 ISnapshotManager 实现此方法
            _snapshotManager.NotifyBatchComplete();
        }

        private void OnSentinelChanged(object sender, FileSystemEventArgs e)
        {
            // 只有在监控状态下才响应变动，防止扫描时的 IO 干扰
            if (_currentState != EngineState.Monitoring) return;

            if (e.Name.Equals("dsnctrl.ini", StringComparison.OrdinalIgnoreCase))
            {
                _versionCoordinator.TriggerNewVersion();
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_currentState != EngineState.Monitoring) return;

            // 寻找匹配该文件的模板
            var template = _templates.FirstOrDefault(t =>
                Regex.IsMatch(e.Name, t.FileNamePattern) ||
                Regex.IsMatch(Path.GetFileName(e.FullPath), t.FileNamePattern));

            if (template != null)
            {
                // 获取当前活动版本号
                string versionId = _versionCoordinator.GetCurrentVersion();
                _orchestrator.ProcessFile(e.FullPath, versionId, template);
            }
        }

        private void UpdateState(EngineState newState)
        {
            _currentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}