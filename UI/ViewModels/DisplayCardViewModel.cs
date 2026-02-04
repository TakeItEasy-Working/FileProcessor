using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;
using UI.Models;

namespace UI.ViewModels
{
    /// <summary>
    /// 数据展示卡片视图模型：负责单个数据块的配置、过滤与数据渲染逻辑。
    /// 它是 UI 层最活跃的组件，处理用户对特定文件和数据块的查看请求。
    /// </summary>
    public partial class DisplayCardViewModel : ViewModelBase
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly MainViewModel _mainVM;

        /// <summary> 用户在界面 ComboBox 中选择的源文件名（例如：wmass.out） </summary>
        [ObservableProperty] private string? _sourceFileName;

        /// <summary> 用户选择的具体数据块名称（例如：“各层刚心、偏心率计算”） </summary>
        [ObservableProperty] private string? _targetBlockName;

        /// <summary> 
        /// 卡片的工作模式：
        /// FollowLatest - 永远显示最新解析的数据
        /// SyncGlobal   - 跟随 MainViewModel 的全局版本切换
        /// ManualLock   - 锁定在某个特定版本，不随外部变化
        /// </summary>
        [ObservableProperty] private CardWorkMode _mode = CardWorkMode.SyncGlobal;

        /// <summary> 当前卡片展示的最终解析结果数据（直接绑定到 DataGrid） </summary>
        [ObservableProperty] private ProcessedResult? _currentData;

        /// <summary> UI 卡片的显示宽度，支持用户在界面上动态调整 </summary>
        [ObservableProperty] private double _cardWidth = 350;

        /// <summary> 
        /// 卡片私有的可选文件名列表。
        /// 当 MainViewModel 发现新文件时，会同步更新此集合。
        /// </summary>
        public ObservableCollection<string> AvailableFiles { get; set; } = new();

        /// <summary> 
        /// 卡片私有的可选数据块名称列表。
        /// </summary>
        public ObservableCollection<string> AvailableBlocks { get; set; } = new();

        /// <summary>
        /// 初始化卡片实例。
        /// </summary>
        /// <param name="snapshotManager">内核快照管理服务</param>
        /// <param name="versionCoordinator">版本协调服务</param>
        /// <param name="mainVM">父级 MainViewModel 引用，用于获取全局缓存数据</param>
        public DisplayCardViewModel(
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator,
            MainViewModel mainVM)
        {
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;
            _mainVM = mainVM;

            // 订阅内核数据更新事件
            _snapshotManager.DataUpdated += OnDataReceived;

            // 新增：初始化后立即尝试拉取一次存量数据
            RefreshData();
        }

        /// <summary>
        /// 清空卡片的当前显示内容。
        /// </summary>
        public void ClearData()
        {
            CurrentData = null;
        }

        /// <summary>
        /// 当用户更改了目标数据块名称时触发。
        /// 修复 CS8826：确保签名与 ObservableProperty 生成的 partial 方法一致。
        /// </summary>
        partial void OnTargetBlockNameChanged(string? value) => RefreshData();

        /// <summary>
        /// 当用户更改了来源文件名时触发。
        /// 修复 CS8826：确保签名与 ObservableProperty 生成的 partial 方法一致。
        /// </summary>
        partial void OnSourceFileNameChanged(string? value) => RefreshData();

        /// <summary>
        /// 当卡片工作模式改变时触发。
        /// </summary>
        partial void OnModeChanged(CardWorkMode value) => RefreshData();

        /// <summary>
        /// 核心逻辑：刷新卡片数据。
        /// 修复 CS1061：不再直接调用内核缺失的 GetSnapshot 方法，改为从父级 ViewModel 的缓存中检索。
        /// </summary>
        private void RefreshData()
        {
            // 基础校验：如果没有指定要看哪个块，则无需检索
            if (string.IsNullOrEmpty(TargetBlockName)) return;

            // 1. 确定目标版本 ID
            string? versionId = Mode switch
            {
                // 全局同步模式：使用主界面当前选中的版本 ID
                CardWorkMode.SyncGlobal => _mainVM.SelectedVersionId,

                // 跟随最新模式：从主界面的版本列表中取第一个（即最新的）
                CardWorkMode.FollowLatest => _mainVM.AllVersionIds.FirstOrDefault(),

                // 锁定模式：保持当前数据的版本 ID 不变
                CardWorkMode.ManualLock => CurrentData?.VersionId,

                _ => _mainVM.SelectedVersionId
            };

            // 2. 执行检索
            if (versionId != null)
            {
                // 通过调用 MainViewModel 提供的桥接方法获取 UI 层缓存的数据
                var snapshot = _mainVM.TryGetCachedSnapshot(versionId, TargetBlockName);

                if (snapshot != null)
                {
                    CurrentData = snapshot;
                }
            }
        }

        /// <summary>
        /// 内核数据推送回调。
        /// </summary>
        /// <param name="result">内核刚刚解析完成的数据块结果</param>
        private void OnDataReceived(ProcessedResult result)
        {
            // 过滤：只有当新解析的块名与本卡片配置的块名一致时才处理
            if (result.BlockName == TargetBlockName)
            {
                // 逻辑判断：是否需要立即更新 UI
                bool shouldUpdate = Mode switch
                {
                    CardWorkMode.FollowLatest => true, // 最新模式始终更新
                    CardWorkMode.SyncGlobal => result.VersionId == _mainVM.SelectedVersionId, // 仅同步全局选中的版本
                    CardWorkMode.ManualLock => false, // 锁定模式不随推送更新
                    _ => false
                };

                if (shouldUpdate)
                {
                    // 使用 Dispatcher 确保在 UI 线程刷新，因为 DataUpdated 来自内核后台线程
                    System.Windows.Application.Current.Dispatcher.Invoke(() => RefreshData());
                }
            }
        }

        /// <summary>
        /// 关闭卡片命令：调用父级 MainViewModel 的移除逻辑。
        /// </summary>
        [RelayCommand]
        private void Close() => _mainVM.RemoveCard(this);
    }
}