using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Mediator.Models;

namespace FileProcessor.Mediator
{
    /// <summary>
    /// 数据协调器：连接解析引擎与 UI 展现层的枢纽。
    /// 负责：插槽路由、多塔过滤、UI 友好型数据包装。
    /// <para>职责：</para>
    /// <list type="bullet">
    /// <item>管理 8 个插槽的配置映射。</item>
    /// <item>执行“行级过滤”以支持单塔/多塔视图切换。</item>
    /// <item>作为视图数据的唯一提供者，屏蔽核心层原始结构。</item>
    /// </list>
    /// </summary>
    public class DataCoordinator
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly List<SlotConfiguration> _slots = new();

        /// <summary>
        /// 初始化协调器，并预置 8 个空白插槽。
        /// </summary>
        /// <param name="snapshotManager">核心层的快照管理器实例</param>
        public DataCoordinator(ISnapshotManager snapshotManager)
        {
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));

            // 初始化 0-7 号插槽
            for (int i = 0; i < 8; i++)
            {
                _slots.Add(new SlotConfiguration { SlotIndex = i });
            }
        }

        #region 插槽配置管理

        /// <summary>
        /// 更新指定插槽的绑定源。
        /// </summary>
        /// <param name="slotIndex">插槽索引 (0-7)</param>
        /// <param name="fileName">文件名 (如 wmass.out)</param>
        /// <param name="blockName">数据块名</param>
        public void UpdateSlot(int slotIndex, string fileName, string blockName)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;

            var slot = _slots[slotIndex];
            slot.TargetFileName = fileName;
            slot.TargetBlockName = blockName;
        }

        /// <summary>
        /// 获取当前的插槽配置列表
        /// </summary>
        public IEnumerable<SlotConfiguration> GetSlots() => _slots;

        #endregion

        #region 核心数据获取与清洗

        /// <summary>
        /// 获取指定插槽在特定版本下的视图数据。
        /// <para>核心逻辑：根据 Protocol V1.1 执行行级过滤。</para>
        /// </summary>
        /// <param name="slotIndex">插槽索引</param>
        /// <param name="versionId">版本 ID (如 Batch_2026...)</param>
        /// <param name="towerSelection">UI 选择的Tower ("1", "2" 或 "All")</param>
        /// <returns>清洗后的 ProcessedResult 对象，若无数据则返回 null</returns>
        public ProcessedResult? GetDisplayData(int slotIndex, string versionId, string towerSelection)
        {
            // 1. 校验插槽配置
            if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
            var config = _slots[slotIndex];
            if (!config.IsActive) return null;

            // 2. 从核心层获取原始数据（包含所有塔的混合数据）
            // 注意：这里我们获取的是 snapshotManager 中的缓存对象，严禁直接修改它！
            var rawResult = _snapshotManager.GetSpecificBlock(versionId, config.TargetFileName!, config.TargetBlockName!);

            if (rawResult == null) return null;

            // 3. 根据Tower选择执行过滤或聚合
            return ApplyTowerFilter(rawResult, towerSelection);
        }

        /// <summary>
        /// 辅助方法：探测当前数据块中包含哪些Tower。
        /// <para>用于 UI 动态填充“Tower”下拉框。</para>
        /// </summary>
        public IEnumerable<string> GetAvailableTowers(int slotIndex, string versionId)
        {
            // 复用获取逻辑，暂时传 "All" 以拿到全量数据
            var result = GetDisplayData(slotIndex, versionId, "All");
            if (result == null || !result.Rows.Any()) return Enumerable.Empty<string>();

            // 扫描所有行，提取Tower并去重
            return result.Rows
                .Select(row => row.TryGetValue("Tower", out var val) ? val : null)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .OrderBy(v => v)!;
        }

        #endregion

        #region 内部私有逻辑 (Row-Level Filtering)

        /// <summary>
        /// 执行Tower过滤逻辑 (Protocol V1.1 实现)
        /// </summary>
        private ProcessedResult ApplyTowerFilter(ProcessedResult original, string towerId)
        {
            // 场景 A: 用户选择 "All"，直接透传原始数据
            // 核心层的数据本来就是混合的，所以不需要做任何合并操作
            if (string.Equals(towerId, "All", StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }

            // 场景 B: 用户选择特定Tower (如 "1")
            // 我们需要创建一个新的 Result 对象 (Deep Clone 模式)，只包含过滤后的行

            // 1. 寻找Tower列
            const string towerKey = "Tower";
            var filteredRows = original.Rows
                .Where(row => row.TryGetValue(towerKey, out var val) && val == towerId)
                .ToList();

            // 返回一个新的对象，共享列定义和元数据，但拥有独立的行集合 
            return new ProcessedResult
            {
                StandardBlockName = original.StandardBlockName,
                RawBlockName = original.RawBlockName,
                DisplayName = original.DisplayName,
                Category = original.Category,
                VersionId = original.VersionId,
                SourceFileName = original.SourceFileName,
                ProcessTime = original.ProcessTime,
                OriginHash = original.OriginHash,

                // 关键点：现在可以赋值了！
                Columns = original.Columns,
                Metadata = original.Metadata,
                Rows = filteredRows
            };
        }
        #endregion
    }
}