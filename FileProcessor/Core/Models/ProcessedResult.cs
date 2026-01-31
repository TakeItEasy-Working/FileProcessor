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

        public DateTime ProcessTime { get; init; } = DateTime.Now;
    }

    public record ColumnDefinition(
        string Key,
        string HeaderText,
        string Unit = "",
        bool IsNumeric = true
    );
}