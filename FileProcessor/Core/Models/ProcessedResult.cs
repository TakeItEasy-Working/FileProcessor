namespace FileProcessor.Core.Models
{
    public class ProcessedResult
    {
        // 1. 身份识别
        public string BlockName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty; // 更加友好的中文名
        public string Category { get; init; } = "常规";          // 方便 UI 分组

        // 2. 表格化数据：Dictionary 的 Key 对应列名，Value 对应单元格内容
        public List<Dictionary<string, string>> Rows { get; init; } = new();

        // 3. 列元数据：这是 UI 自动化生成表头的关键
        public List<ColumnDefinition> Columns { get; init; } = new();

        // 4. 扩展元数据：预留给图表、文件指纹、或特定业务标志
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>
        /// 解析完成的时间戳
        /// </summary>
        public DateTime ProcessTime { get; init; } = DateTime.Now;

        // --- 新增版本追踪字段 ---
        /// <summary>
        /// 关联的版本 ID（由哨兵触发生成）
        /// </summary>
        public string VersionId { get; set; } = "Live";

        /// <summary>
        /// 原始文件的哈希值，用于唯一标识数据状态
        /// </summary>
        public string OriginHash { get; set; } = string.Empty;
    }

    public record ColumnDefinition(
        string Key,
        string HeaderText,
        string Unit = "",
        bool IsNumeric = true
    );
}