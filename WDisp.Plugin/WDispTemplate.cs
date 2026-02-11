using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WDisp.Plugin
{
    /// <summary>
    /// 位移输出文件（wdisp.out）的切块模板类。
    /// 识别以 "====" 包裹的工况名称作为数据块的分界。
    /// </summary>
    [FileProcessorPlugin(@"wdisp.out", "设计结果")]
    public class WDispTemplate : BaseFileTemplate
    {
        /// <summary>
        /// 将位移输出文件切分为多个工况数据块。
        /// </summary>
        /// <param name="lines">文件全行内容</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>切分后的原始数据块集合</returns>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            // wdisp.out 的工况分隔符特征为连续的等号
            string marker = "===";

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                // 识别模式： ==== 工况名称 ====
                if (line.Contains(marker))
                {
                    // 1. 结算当前正在收集的块
                    if (currentBlockLines.Count > 0)
                    {
                        yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                    }

                    // 2. 提取新标题：去掉 ==== 后两端的空白
                    currentTitle = line.Replace(marker, "").Trim();

                    // 3. 重置容器，准备接收新工况数据
                    currentBlockLines = new List<string>();
                    startLineNumber = i + 1;
                    continue;
                }

                currentBlockLines.Add(line);
            }

            // 结算最后一个工况块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}