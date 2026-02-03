using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using UI.ViewModels;

namespace UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ProjectMonitorService _monitorService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;


        // 记录上一次加载的路径，用于去重检查
        private string _lastActivePath = string.Empty;

        // --- 属性绑定 ---
        [ObservableProperty] private string _projectRootPath = string.Empty;
        [ObservableProperty] private string? _selectedVersionId;

        // 全局可用的版本号列表
        public ObservableCollection<string> AllVersionIds { get; } = new();

        // UI 上的卡片集合
        public ObservableCollection<DisplayCardViewModel> DisplayCards { get; } = new();

        public MainViewModel(
            ProjectMonitorService monitorService,
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator)
        {
            _monitorService = monitorService;
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 1. 监听单条数据更新：用于增量更新版本列表
            _snapshotManager.DataUpdated += (res) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (!AllVersionIds.Contains(res.VersionId))
                    {
                        AllVersionIds.Insert(0, res.VersionId);
                    }
                });
            };

            // 2. 监听批处理完成：初始扫描结束后的动作
            _snapshotManager.BatchUpdated += OnInitialScanFinished;

            // --- 新增：程序启动时自动添加两个默认卡片 ---
            AddTestCard();
        }

        /// <summary>
        /// 当 ProjectRootPath 属性发生变化时（由 CommunityToolkit 自动触发）
        /// </summary>
        partial void OnProjectRootPathChanged(string value)
        {
            HandlePathChange(value);
        }

        private void HandlePathChange(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath) || !Directory.Exists(newPath)) return;

            // 如果路径没变，不做任何操作
            string fullPath = Path.GetFullPath(newPath);
            if (fullPath.Equals(_lastActivePath, StringComparison.OrdinalIgnoreCase)) return;

            // 如果已有卡片，切换路径前提示用户
            if (DisplayCards.Any())
            {
                var result = MessageBox.Show(
                    "切换项目路径将清除当前卡片的所有历史记录，是否继续？",
                    "项目切换确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    _projectRootPath = _lastActivePath;
                    OnPropertyChanged(nameof(ProjectRootPath));
                    return;
                }
            }

            // --- 执行切换逻辑 ---
            _lastActivePath = fullPath;

            // 清理 UI 列表
            AllVersionIds.Clear();

            IsBusy = true; // 表示正在初始化

            // 启动内核
            _monitorService.Start(fullPath);
        }

        private void OnInitialScanFinished()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsBusy = false;

                // 强制刷新一次
                OnPropertyChanged(nameof(IsBusy));

                // 初始扫描完成后，强制所有卡片刷新到最新状态
                foreach (var card in DisplayCards)
                {
                    card.RefreshData();
                }
            });
            // MessageBox.Show("扫描完成！"); // 调试稳定后可移除
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            _monitorService.Stop();
            IsBusy = false;
        }

        /// <summary>
        /// 添加演示卡片：现在改为初始化带有下拉选项的通用卡片
        /// </summary>
        [RelayCommand]
        private void AddTestCard()
        {
            // 1. 创建 WMass 演示卡片
            var wmassCard = new DisplayCardViewModel(_snapshotManager, _versionCoordinator);

            // 填充可选文件列表
            wmassCard.AvailableFiles.Add("wmass.out");
            wmassCard.AvailableFiles.Add("wpj.out");

            // 填充可选块名列表 (使用 WMassProcessor.cs 中的真实业务名称)
            wmassCard.AvailableBlockNames.Add("各层刚心、偏心率、相邻层侧移刚度比等计算信息");
            wmassCard.AvailableBlockNames.Add("楼层位移总结");

            // 设置初始默认值
            wmassCard.SourceFileName = "wmass.out";
            wmassCard.TargetBlockName = "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
            wmassCard.Mode = Models.CardWorkMode.FollowLatest;

            // 2. 创建 WPJ 演示卡片
            var wpjCard = new DisplayCardViewModel(_snapshotManager, _versionCoordinator);
            wpjCard.AvailableFiles.Add("wmass.out");
            wpjCard.AvailableFiles.Add("wpj.out");
            wpjCard.AvailableBlockNames.Add("各层刚心、偏心率、相邻层侧移刚度比等计算信息");
            wpjCard.AvailableBlockNames.Add("楼层位移总结");

            wpjCard.SourceFileName = "wpj.out";
            wpjCard.TargetBlockName = "楼层位移总结";
            wpjCard.Mode = Models.CardWorkMode.FollowLatest;

            // 加入集合展示
            DisplayCards.Add(wmassCard);
            DisplayCards.Add(wpjCard);
        }

        [RelayCommand]
        private async Task SimulateYjkOutput()
        {
            if (string.IsNullOrWhiteSpace(ProjectRootPath)) return;

            string root = Path.GetFullPath(ProjectRootPath);
            string designDir = Path.Combine(root, "设计结果");

            try
            {
                if (!Directory.Exists(designDir)) Directory.CreateDirectory(designDir);

                // 模拟 YJK 典型的写文件顺序
                await File.WriteAllTextAsync(Path.Combine(root, "dsnctrl.ini"), $"RunID={DateTime.Now.Ticks}");
                await Task.Delay(500);

                // 模拟真实的块结构（带上标识符），以便让你的 Processor 能够识别
                string wmassContent = "**** 各层刚心、偏心率、相邻层侧移刚度比等计算信息 ****\n" +
                                     " 层号   X刚心   Y刚心\n" +
                                     $"  1    {new Random().Next(10, 50)}    {new Random().Next(10, 50)}\n" +
                                     "========================================";

                await File.WriteAllTextAsync(Path.Combine(designDir, "wmass.out"), wmassContent);
                await File.WriteAllTextAsync(Path.Combine(designDir, "wpj.out"), "楼层位移总结\n层号: 1  位移: 1.2mm");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"模拟失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "请选择 YJK 项目根目录",
                InitialDirectory = string.IsNullOrWhiteSpace(ProjectRootPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : ProjectRootPath
            };

            if (dialog.ShowDialog() == true)
            {
                ProjectRootPath = dialog.FolderName;
            }
        }
    }
}