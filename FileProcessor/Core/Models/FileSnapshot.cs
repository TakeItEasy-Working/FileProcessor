using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 处理结果快照：包含一次文件处理后的所有数据
    /// </summary>
    public record FileSnapshot(
        Guid Id,
        string FileName,
        DateTime ProcessedTime,
        long FileHash,
        IReadOnlyDictionary<string, object> DataBlocks
    );
}
