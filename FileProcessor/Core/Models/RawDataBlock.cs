using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 原始数据块：由 Parser 拆分出来的最小文本单元
    /// </summary>
    public record RawDataBlock(
     string BlockName,      // 由 Template.IdentifyBlockName 确定
     string[] RawLines,     // 由 Parser 根据边界截取
     int StartLineNumber,   // 溯源用：起始行号
     string SourceFileName  // 溯源用：源文件
 );
}
