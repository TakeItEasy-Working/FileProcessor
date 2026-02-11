namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 版本协调器接口：严格管理计算任务的开启与闭环状态。
    /// </summary>
    public interface IVersionCoordinator
    {
        /// <summary>
        /// 当前关联的版本 ID。
        /// 批次模式下返回正式 ID (如 Batch_2023...)；
        /// 实时模式下生成时间戳 ID (如 Live_2023...)。
        /// </summary>
        string CurrentVersionId { get; }

        /// <summary>
        /// 指示当前是否正处于从 check.out 到 mainjss.out 的录制周期内。
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// 启动一个正式的批次版本。
        /// </summary>
        /// <param name source="source">触发来源（如哨兵文件名）</param>
        void StartNewBatch(string source);

        /// <summary>
        /// 显式结束当前批次并触发封版。
        /// </summary>
        /// <param name source="source">结束来源</param>
        void CommitCurrentBatch(string source);

        /// <summary>
        /// 生成一个新的实时时间戳版本号。
        /// 用于非标文件的变化追踪。
        /// </summary>
        string GenerateLiveVersionId();

        /// <summary>
        /// 当批次被 mainjss.out 正式闭环后触发。
        /// </summary>
        event Action<string> VersionCommitted;

        /// <summary>
        /// 当单个实时任务处理完成后触发，通知 UI 进行局部刷新。
        /// </summary>
        event Action<string, string> LiveUpdateProcessed;
    }
}