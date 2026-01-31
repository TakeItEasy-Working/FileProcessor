using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 处理结果快照：包含一次文件处理后的所有数据
    /// </summary>
    public record FileSnapshot
    {
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string FileHash { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.Now;

        // 存储加工后的成品
        public Dictionary<string, ProcessedResult> DataBlocks { get; init; } = new();

        // 辅助方法：UI 可以直接调用，无需自己去查字典
        public IEnumerable<string> GetAvailableBlockNames() => DataBlocks.Keys;

        public bool IsValid => DataBlocks.Any();
    }
}
