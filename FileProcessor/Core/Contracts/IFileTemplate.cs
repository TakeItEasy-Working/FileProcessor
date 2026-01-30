using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Contracts
{
    public interface IFileTemplate
    {
        string FileNamePattern { get; }

        /// <summary>
        /// 识别块名：只在非忽略行上运行
        /// </summary>
        string? IdentifyBlockName(string currentLine, string nextLine);

        /// <summary>
        /// 定义哪些行不参与边界识别，也不进入数据块内容
        /// 例如：空白行、全局注释、页码标记
        /// </summary>
        bool IsIgnorableLine(string line);

        /// <summary>
        /// 定义块的结束标志（可选）
        /// 如果返回 null，则默认下一个块的开始即为当前块的结束
        /// </summary>
        bool IsEndOfBlock(string line) => false;
    }
}
