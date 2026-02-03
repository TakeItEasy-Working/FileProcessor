using FileProcessor.Core.Models;

namespace FileProcessor.Core.Contracts
{
    public interface ISnapshotManager
    {
        /// <summary>
        /// 存储解析出的结果
        /// </summary>
        /// <param name="result">ProcessedResult类型</param>
        void AddSnapshot(ProcessedResult result);

        /// <summary>
        /// 清理当前项目数据，为切换路径做准备
        /// </summary>
        void Clear();

        /// <summary>
        /// 主动触发批处理完成事件
        /// </summary>
        void NotifyBatchComplete();

        /// <summary>
        /// 获取特定文件块的所有历史版本（用于 UI 历史对比）
        /// </summary>
        /// <param name="blockName"></param>
        /// <returns></returns>
        IEnumerable<ProcessedResult> GetHistory(string blockName);

        /// <summary>
        /// 获取特定版本下的所有结果（用于 UI 版本切换）
        /// </summary>
        /// <param name="versionId"></param>
        /// <returns></returns>
        IEnumerable<ProcessedResult> GetResultsByVersion(string versionId);

        /// <summary>
        /// 获取某个块在某个版本下的具体结果
        /// </summary>
        /// <param name="versionId"></param>
        /// <param name="blockName"></param>
        /// <returns></returns>
        ProcessedResult? GetResult(string versionId, string blockName);

        /// <summary>
        /// 当有新的数据块解析完成并存入仓库时触发
        /// </summary>
        event Action<ProcessedResult> DataUpdated;

        /// <summary>
        /// 批处理完成通知（用于 Initial Scan 结束后一次性刷新 UI）
        /// </summary>
        event Action BatchUpdated;
    }
}