using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.DebugHelpers;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using System.Collections.ObjectModel;
using MessageBox = System.Windows.MessageBox;

namespace FileProcessor.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DataCoordinator _coordinator;
        private readonly ProjectMonitorService _monitor;
        private readonly IVersionCoordinator _versionCoordinator; // 新增：直接引用版本协调器

        public MainViewModel(DataCoordinator coordinator, ProjectMonitorService monitor, IVersionCoordinator versionCoordinator)
        {
            _coordinator = coordinator;
            _monitor = monitor;
            _versionCoordinator = versionCoordinator;

            _versionCoordinator.VersionCommitted += OnVersionCommitted;

            // 初始化 8 个插槽
            for (int i = 0; i < 8; i++)
                Slots.Add(new SlotViewModel(_coordinator, i));

            Log.Debug("[MainViewModel] 初始化成功...");
            StatusText = "就绪。请选择项目目录...";
            StatusColor = "#9E9E9E";
        }

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public ObservableCollection<string> VersionHistory { get; } = new();

        [ObservableProperty] private string? _projectPath;
        [ObservableProperty] private string? _selectedVersion;
        [ObservableProperty] private string _currentTower = "All";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _statusColor = "";

        [RelayCommand]
        private void SelectProject()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectPath = dialog.SelectedPath;
                StartProjectMonitoring(dialog.SelectedPath);
            }
        }

        private async void StartProjectMonitoring(string path)
        {
            try
            {
                StatusText = "正在初始化引擎...";
                StatusColor = "#F39C12"; // 橙色表示处理中

                // 清理旧状态
                VersionHistory.Clear();
                VersionHistory.Add("LIVE 实时状态");

                // 1. 启动监控
                // 由于我们在内部实现了静默扫描，这里可以用 Task.Run 跑防止 UI 彻底卡死
                await Task.Run(() => _monitor.StartScanning(path));

                // 注意：这里不需要写 while 循环等待了！
                // 因为扫描结束后，Orchestrator 会触发 Commit("INITIAL_SCAN")
                // 从而触发我们构造函数里绑定的 OnVersionCommitted 方法

                StatusText = $"监控运行中: {System.IO.Path.GetFileName(path)}";
                StatusColor = "#2ECC71"; // 绿色表示正常
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动监控失败: " + ex.Message);
                StatusText = "初始化失败";
                StatusColor = "#E74C3C";
            }
        }

        /// <summary>
        /// 当引擎提交新版本（如 INITIAL_SCAN 或 Batch_xxx）时触发
        /// </summary>
        private void OnVersionCommitted(string versionId)
        {
            // 切换到 UI 线程更新集合
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!VersionHistory.Contains(versionId))
                {
                    VersionHistory.Add(versionId);
                }

                // 如果是第一次扫描完成，或者当前处于 LIVE 模式，则强制触发一次刷新
                if (versionId == "INITIAL_SCAN")
                {
                    SelectedVersion = "INITIAL_SCAN";
                }

                // 刷新所有插槽
                foreach (var slot in Slots)
                {
                    slot.RefreshFileList();
                }
            });
        }

        partial void OnSelectedVersionChanged(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            _coordinator.SwitchViewVersion(value);
            // 切换版本后刷新插槽文件列表
            foreach (var slot in Slots) slot.RefreshFileList();
        }

        partial void OnCurrentTowerChanged(string value)
        {
            for (int i = 0; i < Slots.Count; i++)
                _coordinator.SetTowerFilter(i, value);
        }
    }
}