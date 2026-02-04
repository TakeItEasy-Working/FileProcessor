using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace UI.ViewModels
{
    /// <summary>
    /// 主界面视图模型：负责协调内核服务与 UI 状态同步。
    /// 该类作为 UI 的中枢，维护全局资源列表，并为动态生成的卡片提供数据缓存支持。
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ProjectMonitorService _monitorService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private string _lastActivePath = string.Empty;

        /// <summary> 
        /// 本地数据快照缓存。
        /// 由于 ISnapshotManager 接口目前仅提供推送（事件），不提供拉取（查询）方法，
        /// UI 层通过此字典维护一份已解析数据的副本，以支持卡片在初始化或切换版本时的即时数据获取。
        /// </summary>
        /// <remarks> 键格式: "{VersionId}_{BlockName}" </remarks>
        private readonly Dictionary<string, ProcessedResult> _localSnapshotCache = new();

        /// <summary> 当前监控的项目根目录路径 </summary>
        [ObservableProperty] private string _projectRootPath = string.Empty;

        /// <summary> 全局当前选中的版本 ID </summary>
        [ObservableProperty] private string? _selectedVersionId;

        /// <summary> 界面底部状态栏显示的描述文本 </summary>
        [ObservableProperty] private string _statusText = "准备就绪";

        /// <summary> 指示系统是否处于忙碌状态（如初始扫描中） </summary>
        [ObservableProperty] private bool _isBusy;

        /// <summary> 当前项目发现的所有历史版本 ID 集合 </summary>
        public ObservableCollection<string> AllVersionIds { get; } = new();

        /// <summary> 当前项目中已发现的所有物理文件名集合 </summary>
        public ObservableCollection<string> AvailableFiles { get; } = new();

        /// <summary> 当前项目中所有已注册或已发现的数据块名称集合 </summary>
        public ObservableCollection<string> AvailableBlocks { get; } = new();

        /// <summary> 当前界面上加载的所有分析卡片视图模型集合 </summary>
        public ObservableCollection<DisplayCardViewModel> DisplayCards { get; } = new();

        /// <summary>
        /// 初始化 MainViewModel 实例。
        /// </summary>
        /// <param name="monitorService">内核监控服务</param>
        /// <param name="snapshotManager">快照管理器</param>
        /// <param name="versionCoordinator">版本协调器</param>
        public MainViewModel(
            ProjectMonitorService monitorService,
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator)
        {
            _monitorService = monitorService;
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 订阅内核数据更新事件，将其存入本地 UI 缓存
            _snapshotManager.DataUpdated += OnGlobalDataUpdated;

            // 订阅内核状态改变事件，并将其映射为 UI 文本
            // 修复 CS0029: 通过 MapEngineStateToText 处理 EngineState 到 string 的转换
            _monitorService.StateChanged += (state) =>
            {
                StatusText = MapEngineStateToText(state);
            };

            AddCard();
        }

        /// <summary>
        /// 将内核枚举状态转换为用户友好的中文描述字符串。
        /// </summary>
        /// <param name="state">来自内核的状态对象（通常为 EngineState 枚举）</param>
        /// <returns>映射后的显示文本</returns>
        private string MapEngineStateToText(object state)
        {
            if (state == null) return "未知状态";

            string stateKey = state.ToString() ?? string.Empty;
            return stateKey switch
            {
                "Idle" => "就绪 - 等待操作",
                "Scanning" => "扫描中 - 正在读取项目结构...",
                "Watching" => "监控中 - 实时捕获文件变动",
                "Processing" => "处理中 - 正在解析数据内容...",
                "Error" => "错误 - 内核运行异常",
                _ => $"状态: {stateKey}"
            };
        }

        /// <summary>
        /// 供 DisplayCardViewModel 调用，从本地缓存中安全获取指定版本的数据块。
        /// 该方法弥补了内核接口 ISnapshotManager 缺失 GetSnapshot 方法的问题。
        /// </summary>
        /// <param name="versionId">版本唯一标识</param>
        /// <param name="blockName">数据块名称</param>
        /// <returns>解析结果 ProcessedResult，若未找到则返回 null</returns>
        public ProcessedResult? TryGetCachedSnapshot(string versionId, string blockName)
        {
            string key = $"{versionId}_{blockName}";
            lock (_localSnapshotCache)
            {
                return _localSnapshotCache.TryGetValue(key, out var result) ? result : null;
            }
        }

        /// <summary>
        /// 当项目路径改变时，重置所有状态并重新启动监控。
        /// </summary>
        async partial void OnProjectRootPathChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) return;
            if (value == _lastActivePath) return;
            _lastActivePath = value;

            try
            {
                IsBusy = true;

                // 清理旧数据状态
                lock (_localSnapshotCache) { _localSnapshotCache.Clear(); }
                _snapshotManager.Clear();
                AllVersionIds.Clear();
                AvailableFiles.Clear();
                AvailableBlocks.Clear();

                foreach (var card in DisplayCards) card.ClearData();

                // 启动内核异步监控
                await _monitorService.StartMonitoringAsync(value);
            }
            catch (Exception ex)
            {
                StatusText = "监控启动失败";
                MessageBox.Show($"无法启动项目监控: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        ///// <summary>
        ///// 在界面上新增一个分析卡片，并同步当前的全局资源列表。
        ///// </summary>
        //[RelayCommand]
        //private void AddCard()
        //{
        //    var card = new DisplayCardViewModel(_snapshotManager, _versionCoordinator, this)
        //    {
        //        // 手动同步当前已发现的文件和块，确保新创建的卡片下拉列表不为空
        //        AvailableFiles = new ObservableCollection<string>(this.AvailableFiles),
        //        AvailableBlocks = new ObservableCollection<string>(this.AvailableBlocks)
        //    };
        //    DisplayCards.Add(card);
        //}

        /// <summary>
        /// 移除指定的分析卡片。
        /// </summary>
        public void RemoveCard(DisplayCardViewModel card) => DisplayCards.Remove(card);

        /// <summary>
        /// 处理内核发出的全局数据更新通知。
        /// </summary>
        private void OnGlobalDataUpdated(ProcessedResult res)
        {
            // 1. 存入本地缓存，供后续卡片查询
            string cacheKey = $"{res.VersionId}_{res.BlockName}";
            lock (_localSnapshotCache)
            {
                _localSnapshotCache[cacheKey] = res;
            }

            // 2. 更新 UI 相关的 Observable 集合
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新版本列表
                if (!AllVersionIds.Contains(res.VersionId))
                {
                    AllVersionIds.Insert(0, res.VersionId);
                    if (SelectedVersionId == null) SelectedVersionId = res.VersionId;
                }

                // 更新可用文件列表
                if (res.Metadata.TryGetValue("OriginFile", out var fileName) && fileName is string nameStr)
                {
                    if (!AvailableFiles.Contains(nameStr))
                    {
                        AvailableFiles.Add(nameStr);
                        // 同步更新现有卡片的下拉选项
                        foreach (var card in DisplayCards)
                            if (!card.AvailableFiles.Contains(nameStr)) card.AvailableFiles.Add(nameStr);
                    }
                }

                // 更新可用数据块列表
                if (!AvailableBlocks.Contains(res.BlockName))
                {
                    AvailableBlocks.Add(res.BlockName);
                    // 同步更新现有卡片的下拉选项
                    foreach (var card in DisplayCards)
                        if (!card.AvailableBlocks.Contains(res.BlockName)) card.AvailableBlocks.Add(res.BlockName);
                }
            });
        }

        partial void OnSelectedVersionIdChanged(string? value)
        {
            if (value == null) return;

            // 通知所有处于 SyncGlobal 模式的卡片刷新数据
            foreach (var card in DisplayCards)
            {
                if (card.Mode == UI.Models.CardWorkMode.SyncGlobal)
                {
                    // 注意：由于 RefreshData 是私有的，你可能需要在卡片中将其改为 internal 或 public
                    // 或者通过卡片订阅一个全局事件。
                    card.GetType().GetMethod("RefreshData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(card, null);
                }
            }
        }

        [RelayCommand]
        private void AddCard()
        {
            // 传入 this (MainViewModel) 以便卡片构造时能引用
            var card = new DisplayCardViewModel(_snapshotManager, _versionCoordinator, this);

            // 修正：不再在这里 new 集合，而是在卡片构造函数内部处理
            DisplayCards.Add(card);
        }

        /// <summary>
        /// 弹出文件夹浏览器。
        /// </summary>
        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true) ProjectRootPath = dialog.FolderName;
        }
    }
}