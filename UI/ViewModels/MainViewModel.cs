using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ProjectMonitorService _monitorService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;

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

            // 监听数据仓库，当有新版本出现时更新列表
            _snapshotManager.DataUpdated += (res) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (!AllVersionIds.Contains(res.VersionId))
                    {
                        AllVersionIds.Insert(0, res.VersionId); // 最新的排在最前面
                    }
                });
            };
        }

        // --- 命令绑定 ---

        [RelayCommand]
        private void StartMonitoring()
        {
            if (Directory.Exists(ProjectRootPath))
            {
                _monitorService.Start(ProjectRootPath);
                IsBusy = true; // 借用 ViewModelBase 的 IsBusy 表示监控中
            }
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            _monitorService.Stop();
            IsBusy = false;
        }


        [RelayCommand]
        private void AddTestCard()
        {
            // 创建一个监听 wmass.out 的卡片
            var wmassCard = new DisplayCardViewModel(_snapshotManager, _versionCoordinator)
            {
                SourceFileName = "wmass.out",
                TargetBlockName = "Block_wmass.out", // 必须对应 MockOutTemplate 中的命名规则
                Mode = Models.CardWorkMode.FollowLatest
            };

            // 创建一个监听 wpj.out 的卡片
            var wpjCard = new DisplayCardViewModel(_snapshotManager, _versionCoordinator)
            {
                SourceFileName = "wpj.out",
                TargetBlockName = "Block_wpj.out",
                Mode = Models.CardWorkMode.FollowLatest
            };

            DisplayCards.Add(wmassCard);
            DisplayCards.Add(wpjCard);
        }



        [RelayCommand]
        private async Task SimulateYjkOutput()
        {

            if (string.IsNullOrWhiteSpace(ProjectRootPath))
            {
                MessageBox.Show("请先输入项目路径！");
                return;
            }

            // 强制转换为绝对路径，防止相对路径漂移
            string root = Path.GetFullPath(ProjectRootPath);
            string designDir = Path.Combine(root, "设计结果");

            try
            {
                if (!Directory.Exists(designDir))
                {
                    Directory.CreateDirectory(designDir);
                    MessageBox.Show($"建立文件夹: {designDir}");
                }

                // 打印路径到输出窗口，检查是否正确
                MessageBox.Show($"正在写入到: {root}");

                // 1. 写入哨兵
                await File.WriteAllTextAsync(Path.Combine(root, "dsnctrl.ini"), $"RunID={DateTime.Now.Ticks}");

                // 2. 模拟计算延迟
                await Task.Delay(500);

                // 3. 写入结果
                await File.WriteAllTextAsync(Path.Combine(designDir, "wmass.out"), $"楼层位移: {new Random().Next(1, 100)}mm");
                await File.WriteAllTextAsync(Path.Combine(designDir, "wpj.out"), $"配筋率: {new Random().NextDouble():F2}%");

                MessageBox.Show("文件模拟生成成功，请等待静默期(3s)后查看卡片。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败详情: {ex.Message}");
            }
        }
    }
}
