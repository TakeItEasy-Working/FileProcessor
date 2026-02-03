using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using UI.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Collections.Generic;

namespace UI.ViewModels
{
    /// <summary>
    /// 数据展示卡片视图模型：负责单个数据块的展示逻辑、模式切换及 UI 尺寸控制。
    /// 该类与 MainViewModel 深度联动，支持全局版本同步。
    /// </summary>
    public partial class DisplayCardViewModel : ViewModelBase
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly MainViewModel _parent;

        /// <summary>
        /// 卡片当前监控的源文件名（例如：result.txt）。
        /// 改变时会触发可用数据块列表的刷新。
        /// </summary>
        [ObservableProperty] private string _sourceFileName = string.Empty;

        /// <summary>
        /// 卡片当前关注的数据块名称（例如：底层内力）。
        /// 改变时会触发数据的重新加载。
        /// </summary>
        [ObservableProperty] private string _targetBlockName = string.Empty;

        /// <summary>
        /// 卡片的工作模式：追随最新、同步全局或手动锁定。
        /// </summary>
        [ObservableProperty] private CardWorkMode _mode = CardWorkMode.FollowLatest;

        /// <summary>
        /// 当前卡片展示的解析结果快照。
        /// </summary>
        [ObservableProperty] private ProcessedResult? _currentData;

        /// <summary>
        /// 卡片在看板中的显示宽度，支持 UI 拖拽实时调整。
        /// </summary>
        [ObservableProperty] private double _cardWidth = 480;

        /// <summary>
        /// 供 UI 绑定的文件列表，直接引用自父级 MainViewModel 的全局集合。
        /// </summary>
        public ObservableCollection<string> AvailableFiles => _parent.AvailableFiles;

        /// <summary>
        /// 当前选定文件下发现的所有数据块名称。
        /// </summary>
        public ObservableCollection<string> AvailableBlocks { get; } = new();

        /// <summary>
        /// 初始化卡片视图模型。
        /// </summary>
        /// <param name="snapshotManager">内核快照管理器</param>
        /// <param name="versionCoordinator">版本协同器</param>
        /// <param name="parent">主界面视图模型引用</param>
        public DisplayCardViewModel(
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator,
            MainViewModel parent)
        {
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;
            _parent = parent;

            // 订阅内核数据更新事件
            _snapshotManager.DataUpdated += OnDataUpdated;
        }

        /// <summary>
        /// 关闭命令：请求父容器将本卡片从集合中移除。
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            _parent.RemoveCard(this);
        }

        /// <summary>
        /// 当文件名改变时，自动从历史库中筛选出该文件对应的所有 Block。
        /// </summary>
        partial void OnSourceFileNameChanged(string value)
        {
            RefreshAvailableBlocks();
        }

        /// <summary>
        /// 当目标块名改变时，立即刷新展示的数据。
        /// </summary>
        partial void OnTargetBlockNameChanged(string value)
        {
            RequestRefresh();
        }

        /// <summary>
        /// 当模式改变时，根据新模式的要求重新拉取数据。
        /// </summary>
        partial void OnModeChanged(CardWorkMode value)
        {
            RequestRefresh();
        }

        /// <summary>
        /// 主动刷新请求：根据当前选择的模式从快照仓库中定位最合适的数据。
        /// </summary>
        public void RequestRefresh()
        {
            if (string.IsNullOrEmpty(TargetBlockName)) return;

            // 获取该数据块的所有历史快照记录
            IEnumerable<ProcessedResult> history = _snapshotManager.GetHistory(TargetBlockName);

            ProcessedResult? target = Mode switch
            {
                // 全局同步模式：匹配 MainViewModel 中选中的特定版本 ID
                CardWorkMode.SyncGlobal => history.FirstOrDefault(r => r.VersionId == _parent.SelectedVersionId),

                // 追随最新模式：获取处理时间戳最新的一条
                CardWorkMode.FollowLatest => history.OrderByDescending(r => r.ProcessTime).FirstOrDefault(),

                // 锁定模式：不执行自动刷新，保留当前数据
                _ => CurrentData
            };

            CurrentData = target;
        }

        /// <summary>
        /// 扫描内核仓库，提取出选定文件名下所有的 Block 名称。
        /// </summary>
        private void RefreshAvailableBlocks()
        {
            AvailableBlocks.Clear();

            // 获取所有历史记录并根据元数据中的 OriginFile 进行过滤
            var blocks = _snapshotManager.GetHistory(string.Empty)
                            .Where(r => r.Metadata.ContainsKey("OriginFile") && r.Metadata["OriginFile"].ToString() == SourceFileName)
                            .Select(r => r.BlockName)
                            .Distinct();

            foreach (var b in blocks)
            {
                AvailableBlocks.Add(b);
            }
        }

        /// <summary>
        /// 事件驱动更新：当内核推送新的解析结果时，判断是否属于本卡片的关注范围。
        /// </summary>
        /// <param name="result">新产生的解析结果</param>
        private void OnDataUpdated(ProcessedResult result)
        {
            // 基础检查：Block 名称是否匹配
            if (result.BlockName != TargetBlockName) return;

            bool shouldUpdate = Mode switch
            {
                // 追随最新模式下，任何该 Block 的更新都会导致 UI 刷新
                CardWorkMode.FollowLatest => true,

                // 同步全局模式下，只有新数据的版本号等于全局选定版本号时才更新
                CardWorkMode.SyncGlobal => result.VersionId == _parent.SelectedVersionId,

                // 锁定模式下，拒绝任何推送更新
                _ => false
            };

            if (shouldUpdate)
            {
                // 确保跨线程 UI 更新的安全
                System.Windows.Application.Current.Dispatcher.Invoke(() => CurrentData = result);
            }
        }
    }
}