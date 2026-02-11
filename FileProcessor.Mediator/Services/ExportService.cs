using FileProcessor.Core.Contracts;
using System.Linq;

namespace FileProcessor.Mediator.Services
{
    /// <summary>
    /// 导出服务：跨版本历史数据提取与报表生成。
    /// </summary>
    public class ExportService
    {
        private readonly ISnapshotManager _snapshotManager;

        public ExportService(ISnapshotManager snapshotManager)
        {
            _snapshotManager = snapshotManager;
        }

        /// <summary>
        /// 导出历史趋势报告
        /// </summary>
        /// <param name="blockName">标准化块名</param>
        /// <param name="towerId">塔号</param>
        /// <param name="includeLive">是否包含实时 Live 修改记录</param>
        public void ExportCrossVersionReport(string blockName, string towerId, bool includeLive)
        {
            // 1. 获取所有版本的该块数据
            var history = _snapshotManager.GetHistory(blockName);

            // 2. 根据用户需求决定是否剔除 Live 过程数据
            if (!includeLive)
            {
                history = history.Where(r => !r.VersionId.StartsWith("Live_"));
            }

            // 3. 执行 Tower 过滤
            if (towerId != "All")
            {
                history = history.Where(r =>
                    r.Rows.Any(row => row.TryGetValue("Tower", out var t) && t == towerId));
            }

            // 后续：转换 history 列表并使用 MiniExcel 导出...
        }
    }
}