using FileProcessor.Core.Models;
using System.Collections.Generic;

namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 快照存储接口：定义解析结果的存取契约。
    /// 移除了不必要的事件，遵循“单一职责原则”。
    /// </summary>
    public interface ISnapshotManager
    {
        /// <summary>
        /// 存储单个解析结果
        /// </summary>
        void AddSnapshot(ProcessedResult result);

        /// <summary>
        /// Level 1 查询：获取特定版本下解析过的所有文件名
        /// </summary>
        IEnumerable<string> GetFileNames(string versionId);

        /// <summary>
        /// Level 2 查询：获取特定文件下产生的所有标准化数据块结果
        /// </summary>
        IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName);

        /// <summary>
        /// 精确查询：获取特定版本的特定块数据
        /// </summary>
        ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName);

        /// <summary>
        /// 历史查询：获取同名块在所有版本中的记录（用于对比图表）
        /// </summary>
        IEnumerable<ProcessedResult> GetHistory(string standardBlockName);

        /// <summary>
        /// 获取某个版本下已成功解析的文件总数
        /// </summary>
        int GetParsedFileCount(string versionId);

        /// <summary>
        /// 扫描指定版本中的所有解析数据，提取并去重所有出现的塔号 (Tower)。
        /// 采用 HashSet 确保极速去重，并按照自然排序返回。
        /// </summary>
        /// <param name="versionId">目标版本号</param>
        /// <returns>去重后的塔号集合（如 "1", "2", "3A"）</returns>
        IEnumerable<string> GetAvailableTowers(string versionId);

        /// <summary>
        /// 获取指定版本下的所有解析数据快照
        /// </summary>
        /// <param name="versionId">版本号</param>
        /// <returns>该版本下的全量数据结果</returns>
        IEnumerable<ProcessedResult> GetSnapshotsByVersion(string versionId);

        /// <summary>
        /// 清理所有缓存
        /// </summary>
        void Clear();
    }
}