using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using UI.Models;

namespace UI.ViewModels
{
    public partial class DisplayCardViewModel : ViewModelBase
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;

        // 只要写私有字段并打上标签，系统会自动生成 public string SourceFileName { get; set; }
        [ObservableProperty] private string _sourceFileName = string.Empty;
        [ObservableProperty] private string _targetBlockName = string.Empty;
        [ObservableProperty] private CardWorkMode _mode = CardWorkMode.FollowLatest;
        [ObservableProperty] private ProcessedResult? _currentData;

        public DisplayCardViewModel(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
        {
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 订阅仓库变动
            _snapshotManager.DataUpdated += OnDataUpdated;
        }

        private void OnDataUpdated(ProcessedResult result)
        {
            // 1. 过滤：是否是我要的那个块？
            if (result.BlockName != TargetBlockName) return;

            // 2. 根据模式判断是否需要“吞下”这块数据
            bool shouldUpdate = Mode switch
            {
                CardWorkMode.FollowLatest => true,
                CardWorkMode.SyncGlobal => result.VersionId == _versionCoordinator.CurrentVersionId,
                CardWorkMode.ManualLock => false,
                _ => false
            };

            if (shouldUpdate)
            {
                // UI 线程同步更新
                App.Current.Dispatcher.Invoke(() => CurrentData = result);
            }
        }

        // 利用 [RelayCommand] 自动生成供 UI 按钮绑定的命令
        [RelayCommand]
        private void Refresh()
        {
            // 手动从仓库拉取最新一次的解析结果
            CurrentData = _snapshotManager.GetHistory(TargetBlockName)
                                          .OrderByDescending(r => r.ProcessTime)
                                          .FirstOrDefault();
        }
    }
}
