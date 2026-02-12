using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Mediator.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileProcessor.Mediator
{
    /// <summary>
    /// 数据协调器：UI 层访问核心引擎数据的唯一枢纽。
    /// <para>主要职责：</para>
    /// <list type="bullet">
    /// <item>管理插槽与物理文件的配置映射。</item>
    /// <item>处理版本视图切换（Live 模式与历史回溯）。</item>
    /// <item>执行业务级数据过滤（如按塔号筛选）。</item>
    /// <item>维护插槽生命周期状态（加载中、空数据、就绪）。</item>
    /// </list>
    /// </summary>
    public class DataCoordinator
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly List<SlotConfiguration> _slots = new();

        // [NEW] 私有影子变量：记录协调器感知到的最后一次提交的版本
        // 用于判断当前用户是否正处于“跟随最新结果”的状态
        private string _lastAutoVersionId;

        /// <summary>
        /// 获取或设置当前视图正在观察的版本 ID。
        /// 切换此 ID 会触发所有活跃插槽的数据重载。
        /// </summary>
        public string ActiveViewVersionId { get; private set; }

        public bool IsFollowingLive => ActiveViewVersionId == _lastAutoVersionId;

        /// <summary>
        /// 当某个插槽的数据内容发生更新时触发。
        /// <para>参数 1: 插槽索引 (0-7)；参数 2: 过滤后的结果对象（若无数据则为 null）。</para>
        /// </summary>
        public event Action<int, ProcessedResult?>? SlotDataChanged;

        /// <summary>
        /// 当某个插槽的业务状态（如就绪、加载中）发生变化时触发。
        /// <para>参数 1: 插槽索引 (0-7)；参数 2: 新的状态枚举值。</para>
        /// </summary>
        public event Action<int, SlotStatus>? SlotStatusChanged;

        /// <summary>
        /// 初始化数据协调器实例。
        /// </summary>
        /// <param name="snapshotManager">内核快照管理器，用于数据查询。</param>
        /// <param name="versionCoordinator">内核版本管理器，用于监听封版信号。</param>
        public DataCoordinator(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
        {
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
            _versionCoordinator = versionCoordinator ?? throw new ArgumentNullException(nameof(versionCoordinator));

            // 初始化 8 个预设插槽容器
            for (int i = 0; i < 8; i++) _slots.Add(new SlotConfiguration { SlotIndex = i });

            // 初始视图默认为内核当前最新版本
            ActiveViewVersionId = _versionCoordinator.CurrentVersionId;
            _lastAutoVersionId = ActiveViewVersionId;

            // 订阅内核封版事件：当 YJK 计算产生新文件并封版时，自动刷新 UI
            _versionCoordinator.VersionCommitted += OnVersionCommitted;
        }

        #region UI 交互接口 (Commands)

        /// <summary>
        /// 配置插槽关注的数据源。调用后将立即根据当前 <see cref="ActiveViewVersionId"/> 拉取数据。
        /// </summary>
        /// <param name="index">插槽索引 (0-7)。</param>
        /// <param name="fileName">目标文件名（如 "wdisp.out"）。</param>
        /// <param name="standardBlockName">目标数据块标准 ID。</param>
        public void ConfigureSlot(int index, string fileName, string standardBlockName)
        {
            if (index < 0 || index >= _slots.Count) return;

            var slot = _slots[index];
            slot.TargetFileName = fileName;
            slot.TargetBlockName = standardBlockName;

            RefreshSlot(index);
        }

        /// <summary>
        /// 切换视图观察的版本。常用于左侧版本列表点击切换。
        /// </summary>
        /// <param name="versionId">目标版本 ID。</param>
        public void SwitchViewVersion(string versionId)
        {
            if (string.IsNullOrEmpty(versionId) || ActiveViewVersionId == versionId) return;

            ActiveViewVersionId = versionId;

            // 切换版本后，所有已配置的插槽都需要重新同步数据
            // 注意：一旦调用此方法，ActiveViewVersionId 就会脱离 _lastAutoVersionId
            // 从而进入“手动模式”，直到下一次封版或者用户手动切回最新。
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) RefreshSlot(i);
            }
        }

        /// <summary>
        /// 更新指定插槽的塔号过滤条件。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        /// <param name="towerId">塔号（如 "1", "2"）或 "All" 表示全楼汇总。</param>
        public void SetTowerFilter(int index, string towerId)
        {
            if (index < 0 || index >= _slots.Count) return;

            _slots[index].CurrentTower = towerId;
            RefreshSlot(index);
        }

        /// <summary>
        /// 强制触发指定插槽的数据刷新逻辑。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        public void RefreshSlot(int index)
        {
            var slot = _slots[index];
            if (!slot.IsActive) return;

            UpdateStatus(index, SlotStatus.Loading);

            // 关键点：使用 ActiveViewVersionId 确保视图与后端计算解耦
            var rawData = _snapshotManager.GetSpecificBlock(
                ActiveViewVersionId,
                slot.TargetFileName!,
                slot.TargetBlockName!);

            if (rawData == null)
            {
                UpdateStatus(index, SlotStatus.NoData);
                SlotDataChanged?.Invoke(index, null);
                return;
            }

            // 执行多塔过滤逻辑
            var filteredData = ApplyTowerFilter(rawData, slot.CurrentTower);

            UpdateStatus(index, SlotStatus.Ready);
            SlotDataChanged?.Invoke(index, filteredData);
        }

        #endregion

        #region 元数据发现接口 (Query)

        /// <summary>
        /// 获取当前选定版本中所有已解析的文件名列表。用于填充 UI 下拉框。
        /// </summary>
        /// <returns>文件名集合。</returns>
        public IEnumerable<string> GetAvailableFiles()
        {
            return _snapshotManager.GetFileNames(ActiveViewVersionId);
        }

        /// <summary>
        /// 获取指定文件中包含的所有可用数据块名称。用于填充 UI 的二级下拉框。
        /// </summary>
        /// <param name="fileName">文件名。</param>
        /// <returns>数据块显示名称集合。</returns>
        public IEnumerable<string> GetBlocksForFile(string fileName)
        {
            var results = _snapshotManager.GetResultsByFile(ActiveViewVersionId, fileName);
            return results.Select(r => !string.IsNullOrEmpty(r.DisplayName) ? r.DisplayName : r.RawBlockName).Distinct();
        }

        /// <summary>
        /// 获取指定数据块在所有历史版本中的变化趋势。用于右侧面板绘图。
        /// </summary>
        /// <param name="standardBlockName">数据块标准名。</param>
        /// <returns>按时间排序的历史结果集合。</returns>
        public IEnumerable<ProcessedResult> GetHistoryTrend(string standardBlockName)
        {
            return _snapshotManager.GetHistory(standardBlockName);
        }

        /// <summary>
        /// 获取指定插槽的当前配置信息副本（只读）。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        /// <returns>插槽配置对象。</returns>
        public SlotConfiguration GetSlotInfo(int index) => _slots[index];

        #endregion

        #region 私有辅助逻辑

        /// <summary>
        /// 响应内核推送：当新计算完成时，如果是 Live 视图，则自动同步。
        /// </summary>
        private void OnVersionCommitted(string versionId)
        {
            // 1. 逻辑判断：如果当前用户的视图版本等于我们上次记录的自动版本
            // 说明用户此刻正处于“自动模式”，没有去手动查看老版本。
            bool shouldFollow = (ActiveViewVersionId == _lastAutoVersionId);

            // 2. 更新影子变量，记录最新的版本锚点
            _lastAutoVersionId = versionId;

            // 3. 如果需要跟随，则更新 UI 观察的版本
            if (shouldFollow)
            {
                ActiveViewVersionId = versionId;
            }

            // 4. 无论是否跟随版本，数据刷新是必须的（或者你可以根据业务需求决定是否仅在跟随状态下刷新）
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) RefreshSlot(i);
            }
        }

        /// <summary>
        /// 执行行级过滤。根据 Rows 中的 "Tower" 列筛选目标数据。
        /// </summary>
        private ProcessedResult ApplyTowerFilter(ProcessedResult original, string towerId)
        {
            if (string.Equals(towerId, "All", StringComparison.OrdinalIgnoreCase)) return original;

            return new ProcessedResult
            {
                StandardBlockName = original.StandardBlockName,
                DisplayName = original.DisplayName,
                Category = original.Category,
                SourceFileName = original.SourceFileName,
                VersionId = original.VersionId,
                Columns = original.Columns,
                // 执行内存过滤：仅保留匹配 Tower ID 的行
                Rows = original.Rows.Where(r => r.TryGetValue("Tower", out var v) && v == towerId).ToList(),
                Metadata = original.Metadata
            };
        }

        /// <summary>
        /// 内部状态转换维护。
        /// </summary>
        private void UpdateStatus(int index, SlotStatus status)
        {
            if (_slots[index].Status != status)
            {
                _slots[index].Status = status;
                SlotStatusChanged?.Invoke(index, status);
            }
        }

        #endregion
    }
}