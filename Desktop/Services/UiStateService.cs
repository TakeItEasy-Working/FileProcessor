//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Windows;
//using FileProcessor.Core.Contracts;
//using FileProcessor.Core.Models;
//using FileProcessor.Engine.Services;

//namespace Desktop.Services
//{
//    /// <summary>
//    /// UI 状态服务：作为前端的“版本化内存数据库”。
//    /// 该服务负责将内核中离散的文件快照（FileSnapshot）聚合为 UI 侧的完整版本快照。
//    /// 遵循“拉取”模式，仅在内核明确提交版本（VersionCommitted）后更新 UI 资源池。
//    /// </summary>
//    public class UiStateService : IUiStateService, IDisposable
//    {
//        private readonly ISnapshotManager _snapshotManager;
//        private readonly IVersionCoordinator _versionCoordinator;

//        /// <summary>
//        /// 内部存储：Key 为 VersionId, Value 为该版本下所有文件的 ProcessedResult 集合。
//        /// 结构：Dictionary<VersionId, Dictionary<FileName_BlockName, ProcessedResult>>
//        /// </summary>
//        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProcessedResult>> _versionCache = new();

//        /// <summary>
//        /// 记录每个版本对应的友好显示信息。
//        /// </summary>
//        private readonly ConcurrentDictionary<string, string> _versionDisplayMap = new();

//        /// <summary>
//        /// 全局可用的版本号集合（历史记录），UI 列表绑定源。
//        /// </summary>
//        public ObservableCollection<string> AllVersionIds { get; } = new();

//        /// <summary>
//        /// 全局可用的文件名集合资源池。
//        /// </summary>
//        public ObservableCollection<string> AvailableFiles { get; } = new();

//        /// <summary>
//        /// 全局可用的数据块名称集合资源池。
//        /// </summary>
//        public ObservableCollection<string> AvailableBlocks { get; } = new();

//        /// <summary>
//        /// 数据更新事件：通知订阅者（如 MainViewModel）有新版本产生。
//        /// 参数为新产生的 VersionId。
//        /// </summary>
//        public event Action<string>? DataUpdated;

//        /// <summary>
//        /// 初始化 UI 状态服务。
//        /// </summary>
//        /// <param name="snapshotManager">内核快照管理器</param>
//        /// <param name="versionCoordinator">内核版本协调器</param>
//        public UiStateService(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
//        {
//            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
//            _versionCoordinator = versionCoordinator ?? throw new ArgumentNullException(nameof(versionCoordinator));

//            // 订阅内核版本提交事件：这是 UI 更新的唯一合法入口
//            _versionCoordinator.VersionCommitted += OnVersionCommitted;
//        }

//        /// <summary>
//        /// 当内核 3s 静默期结束，正式提交一个版本时触发。
//        /// 此方法负责将当前所有文件的最新快照“封存”到 UI 缓存中。
//        /// </summary>
//        /// <param name="versionId">内核生成的版本标识符</param>
//        private void OnVersionCommitted(string versionId)
//        {
//            // 1. 获取该时刻所有文件的最新状态
//            // 注意：内核 SnapshotManager 按文件名组织数据，我们需要将其转换为 UI 视角
//            var versionData = new ConcurrentDictionary<string, ProcessedResult>();

//            // 访问内核 SnapshotManager 内部的 history (通过转型或现有接口)
//            if (_snapshotManager is SnapshotManager sm)
//            {
//                // 获取当前所有已处理的文件名
//                var fileNames = AvailableFiles.ToList();

//                // 遍历内核中所有文件的最新快照
//                // 由于我们要“全窗口同步”，我们需要确保新版本包含所有文件的最新状态
//                foreach (var fileName in fileNames)
//                {
//                    var latestFileSnapshot = sm.GetHistory(fileName).FirstOrDefault();
//                    if (latestFileSnapshot != null)
//                    {
//                        foreach (var kvp in latestFileSnapshot.DataBlocks)
//                        {
//                            string blockName = kvp.Key;
//                            ProcessedResult result = kvp.Value;

//                            // 组合键：文件名 + 块名
//                            string storageKey = $"{fileName}_{blockName}";
//                            versionData[storageKey] = result;

//                            // 顺便更新全局资源池列表
//                            UpdateGlobalResourcePool(fileName, blockName);
//                        }
//                    }
//                }
//            }

//            // 2. 存入 UI 缓存
//            _versionCache[versionId] = versionData;

//            // 3. 更新 UI 列表并触发通知
//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                if (!AllVersionIds.Contains(versionId))
//                {
//                    // 新版本插入到首位，方便 UI 默认选中最新
//                    AllVersionIds.Insert(0, versionId);
//                }

//                // 触发自动跟随逻辑
//                DataUpdated?.Invoke(versionId);
//            });
//        }

//        /// <summary>
//        /// 安全地更新全局文件名和块名资源池。
//        /// </summary>
//        private void UpdateGlobalResourcePool(string fileName, string blockName)
//        {
//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                if (!AvailableFiles.Contains(fileName))
//                    AvailableFiles.Add(fileName);

//                if (!AvailableBlocks.Contains(blockName))
//                    AvailableBlocks.Add(blockName);
//            });
//        }

//        /// <summary>
//        /// 获取特定坐标下的数据。
//        /// </summary>
//        /// <param name="versionId">目标版本</param>
//        /// <param name="fileName">目标文件</param>
//        /// <param name="blockName">目标数据块</param>
//        /// <returns>解析结果</returns>
//        public ProcessedResult? GetResult(string versionId, string fileName, string blockName)
//        {
//            if (string.IsNullOrEmpty(versionId) || string.IsNullOrEmpty(fileName)) return null;

//            if (_versionCache.TryGetValue(versionId, out var versionData))
//            {
//                string storageKey = $"{fileName}_{blockName}";
//                if (versionData.TryGetValue(storageKey, out var result))
//                {
//                    return result;
//                }
//            }
//            return null;
//        }

//        /// <summary>
//        /// 清理资源并取消订阅。
//        /// </summary>
//        public void Dispose()
//        {
//            _versionCoordinator.VersionCommitted -= OnVersionCommitted;
//        }
//    }
//}