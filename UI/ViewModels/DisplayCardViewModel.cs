using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.ObjectModel;
using UI.Models;

namespace UI.ViewModels
{
    /// <summary>
    /// 卡片视图模型：支持动态选择文件和数据块，并实时展示解析结果
    /// </summary>
    public partial class DisplayCardViewModel : ViewModelBase
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;

        // --- 属性绑定：UI 交互 ---

        /// <summary>
        /// 当前卡片监视的源文件名（如 wmass.out）
        /// </summary>
        [ObservableProperty]
        private string _sourceFileName = string.Empty;

        /// <summary>
        /// 当前卡片监视的目标数据块名
        /// </summary>
        [ObservableProperty]
        private string _targetBlockName = string.Empty;

        /// <summary>
        /// 卡片工作模式：随动最新、全局同步、手动锁定
        /// </summary>
        [ObservableProperty]
        private CardWorkMode _mode = CardWorkMode.FollowLatest;

        /// <summary>
        /// 当前展示的数据结果
        /// </summary>
        [ObservableProperty]
        private ProcessedResult? _currentData;

        // --- 下拉列表：供 UI 筛选 ---

        /// <summary>
        /// 可选的文件名列表
        /// </summary>
        public ObservableCollection<string> AvailableFiles { get; } = new();

        /// <summary>
        /// 可选的数据块名列表
        /// </summary>
        public ObservableCollection<string> AvailableBlockNames { get; } = new();

        // --- 构造函数 ---

        public DisplayCardViewModel(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
        {
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 订阅仓库变动：当内核解析出新数据时，所有卡片都会收到通知
            _snapshotManager.DataUpdated += OnDataUpdated;
        }

        // --- 属性变更回调 (CommunityToolkit 自动生成) ---

        /// <summary>
        /// 当 SourceFileName 改变时（用户在下拉框选择了新文件）
        /// </summary>
        partial void OnSourceFileNameChanged(string value) => RefreshData();

        /// <summary>
        /// 当 TargetBlockName 改变时（用户在下拉框选择了新块）
        /// </summary>
        partial void OnTargetBlockNameChanged(string value) => RefreshData();

        // --- 核心方法 ---

        /// <summary>
        /// 强制从仓库拉取并刷新当前 UI 数据
        /// </summary>
        public void RefreshData()
        {
            if (string.IsNullOrEmpty(SourceFileName) || string.IsNullOrEmpty(TargetBlockName))
                return;

            // 根据当前选中的 文件名 和 块名，从快照管理器获取最新的解析记录
            // 注意：此处 GetHistory 需要确保能通过文件名过滤（如果接口支持）
            // 暂时沿用原逻辑：按块名查找并取最后一次处理的结果
            var latest = _snapshotManager.GetHistory(TargetBlockName)
                            .OrderByDescending(r => r.ProcessTime)
                            .FirstOrDefault();

            // 如果该数据确实属于当前选中的文件，则更新 UI
            // (注：未来如果 ProcessedResult 包含 OriginFile 属性会更精确)
            CurrentData = latest;
        }

        /// <summary>
        /// 实时更新逻辑：处理内核推送的新数据
        /// </summary>
        private void OnDataUpdated(ProcessedResult result)
        {
            // 1. 过滤：只处理用户当前选中的那个“文件+块”组合
            if (result.BlockName != TargetBlockName) return;

            // 2. 判断是否满足更新模式
            bool shouldUpdate = Mode switch
            {
                CardWorkMode.FollowLatest => true, // 只要是这个块就更新
                CardWorkMode.SyncGlobal => result.VersionId == _versionCoordinator.CurrentVersionId,
                CardWorkMode.ManualLock => false,
                _ => false
            };

            if (shouldUpdate)
            {
                // 切换到 UI 线程更新属性，触发 DataGrid 刷新
                App.Current.Dispatcher.Invoke(() => CurrentData = result);
            }
        }

        /// <summary>
        /// 手动刷新命令
        /// </summary>
        [RelayCommand]
        private void ManualRefresh()
        {
            RefreshData();
        }
    }
}
