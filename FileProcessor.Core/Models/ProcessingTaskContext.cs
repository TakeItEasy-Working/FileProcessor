namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 解析任务上下文：记录待处理文件及其锁定的版本信息。
    /// 在 Batch 模式下，相同路径的上下文会被覆盖，确保只保留最后一次变动。
    /// </summary>
    public record ProcessingTaskContext
    {
        /// <summary>
        /// 待处理文件的完整物理路径
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// 该文件对应的文件名（不含路径）
        /// </summary>
        public string FileName => System.IO.Path.GetFileName(FilePath);

        /// <summary>
        /// 在监控阶段预先计算好的文件 Hash，用于准入检查及结果溯源
        /// </summary>
        public string FileHash { get; init; } = string.Empty;

        /// <summary>
        /// 触发该任务时锁定的版本 ID（如 Batch_20231027_1000）
        /// </summary>
        public string BoundVersionId { get; init; } = "Live";

        /// <summary>
        /// 任务创建的时间戳
        /// </summary>
        public DateTime TriggerTime { get; init; } = DateTime.Now;

        /// <summary>
        /// 标识该任务是否由于 mainjss.out 到达而触发的最终执行
        /// </summary>
        public bool IsFinalCommit { get; init; } = false;
    }
}