using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Services;
using UI.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UI.ViewModels
{
    /// <summary>
    /// 主界面视图模型：负责全局状态管理、项目监控驱动及卡片生命周期控制。
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ProjectMonitorService _monitorService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;

        /// <summary> 当前监控的项目根目录路径 </summary>
        [ObservableProperty] private string _projectRootPath = string.Empty;

        /// <summary> 全局选中的版本 ID，用于所有“同步模式”下的卡片联动 </summary>
        [ObservableProperty] private string? _selectedVersionId;

        /// <summary> 全局发现的所有版本号集合 </summary>
        public ObservableCollection<string> AllVersionIds { get; } = new();

        /// <summary> 当前项目下所有已识别到的文件名列表 </summary>
        public ObservableCollection<string> AvailableFiles { get; } = new();

        /// <summary> 当前主窗体展示的卡片集合 </summary>
        public ObservableCollection<DisplayCardViewModel> DisplayCards { get; } = new();

        /// <summary>
        /// 初始化主界面 ViewModel。
        /// </summary>
        /// <param name="monitorService">内核监控服务</param>
        /// <param name="snapshotManager">数据快照管理器</param>
        /// <param name="versionCoordinator">版本协同器</param>
        public MainViewModel(
            ProjectMonitorService monitorService,
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator)
        {
            _monitorService = monitorService;
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 注册全局数据更新回调，用于动态维护文件列表和版本列表
            _snapshotManager.DataUpdated += OnGlobalDataUpdated;

            // 启动时默认添加一张空卡片
            AddCard();
        }

        /// <summary>
        /// 浏览文件夹并启动监控。
        /// </summary>
        [RelayCommand]
        private async Task BrowseFolder()
        {
            // 使用 Microsoft.Win32.OpenFolderDialog (需要 .NET 8.0+)
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择 YJK 项目根目录",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                ProjectRootPath = dialog.FolderName;

                // 重置状态
                AvailableFiles.Clear();
                AllVersionIds.Clear();

                // 启动内核监控任务
                await _monitorService.StartMonitoringAsync(ProjectRootPath);
            }
        }

        /// <summary>
        /// 向看板添加一个新的数据卡片。
        /// </summary>
        [RelayCommand]
        private void AddCard()
        {
            var card = new DisplayCardViewModel(_snapshotManager, _versionCoordinator, this);
            DisplayCards.Add(card);
        }

        /// <summary>
        /// 从看板中移除指定的卡片（由卡片内部 CloseCommand 回调）。
        /// </summary>
        /// <param name="card">要移除的卡片实例</param>
        public void RemoveCard(DisplayCardViewModel card)
        {
            if (DisplayCards.Contains(card))
            {
                DisplayCards.Remove(card);
            }
        }

        /// <summary>
        /// 模拟内核计算，触发一个新版本的产生。
        /// </summary>
        [RelayCommand]
        private void SimulateRun()
        {
            _versionCoordinator.TriggerNewVersion();
        }

        /// <summary>
        /// 监听内核产生的解析结果，更新全局列表供 UI 选择。
        /// </summary>
        private void OnGlobalDataUpdated(FileProcessor.Core.Models.ProcessedResult res)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. 更新全局版本列表（新版本插入到首位）
                if (!AllVersionIds.Contains(res.VersionId))
                {
                    AllVersionIds.Insert(0, res.VersionId);
                    // 如果当前没选中版本，默认选中最新的
                    if (SelectedVersionId == null) SelectedVersionId = res.VersionId;
                }

                // 2. 从元数据中提取原始文件名，存入可用文件列表
                if (res.Metadata.TryGetValue("OriginFile", out var fileName))
                {
                    var nameStr = fileName.ToString();
                    if (!string.IsNullOrEmpty(nameStr) && !AvailableFiles.Contains(nameStr))
                    {
                        AvailableFiles.Add(nameStr);
                    }
                }
            });
        }

        /// <summary>
        /// 当全局版本切换时，通知所有处于“同步全局”模式下的卡片进行数据回溯。
        /// </summary>
        partial void OnSelectedVersionIdChanged(string? value)
        {
            foreach (var card in DisplayCards.Where(c => c.Mode == CardWorkMode.SyncGlobal))
            {
                card.RequestRefresh();
            }
        }
    }
}