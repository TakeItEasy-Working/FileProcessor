using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 原始数据块：由 Parser 拆分出来的最小文本单元
    /// </summary>
    public class RawDataBlock
    {
        public string BlockName { get; }
        public string[] Lines { get; }
        public int StartLineNumber { get; }
        public string SourceFilePath { get; }
        public string FileHash { get; set; } = string.Empty; // 新增这一行

        public RawDataBlock(string blockName, string[] lines, int startLine, string filePath)
        {
            BlockName = blockName;
            Lines = lines;
            StartLineNumber = startLine;
            SourceFilePath = filePath;
        }
    }
}
