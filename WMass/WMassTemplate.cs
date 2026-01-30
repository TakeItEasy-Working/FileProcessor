using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WMass.Plugin
{
    [FileProcessorPlugin]
    public class WMassTemplate : BaseFileTemplate
    {
        public override string FileNamePattern => @"wmass.out";

        /// <summary>
        /// 增强版拆分逻辑：支持 ****、==== 等多种形式的标题包围线
        /// </summary>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            // 定义可能的边界特征：星号线或等号线
            string[] markers = { "****", "====" };

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                // 识别任何一种边界特征线
                if (markers.Any(m => line.Contains(m)))
                {
                    int headerEnd = i;
                    List<string> headerLines = new List<string>();

                    // 向下嗅探：直到遇到下一行特征线（无论它是星号还是等号）
                    // 逻辑：只要下一行不包含任何标记，且不是文件末尾，就视作标题内容
                    while (headerEnd + 1 < lines.Count && !markers.Any(m => lines[headerEnd + 1].Contains(m)))
                    {
                        headerEnd++;
                        string candidate = lines[headerEnd].Trim();
                        if (!string.IsNullOrWhiteSpace(candidate))
                            headerLines.Add(candidate);
                    }

                    // 如果在两条特征线之间抓到了文字，说明这是一个新块的开始
                    if (headerLines.Count > 0)
                    {
                        // 1. 提交上一个块
                        if (currentBlockLines.Count > 0)
                        {
                            yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                        }

                        // 2. 提取新标题（如果有两行标题，通常取第一行作为 Key）
                        currentTitle = headerLines[0];
                        currentBlockLines = new List<string>();

                        // 数据起始行应该是标题区结束后的那一行
                        startLineNumber = headerEnd + 2;

                        // 3. 跳过整个标题包围区
                        i = headerEnd + 1;
                        continue;
                    }
                }

                // 普通数据行收集
                currentBlockLines.Add(line);
            }

            // 提交最后一个残留块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}