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
        /// 清理所有缓存
        /// </summary>
        void Clear();
    }
}