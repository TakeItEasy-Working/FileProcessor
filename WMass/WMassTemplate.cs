using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace WMass.Plugin
{
    [FileProcessorPlugin]
    public class WMassTemplate : BaseFileTemplate
    {
        // 匹配 YJK 的质量中心与刚度文件
        public override string FileNamePattern => @"wmass.out";

        /// <summary>
        /// 拆分逻辑：自动识别以 **** 或 ==== 包围的标题块
        /// </summary>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            // 特征线定义：识别 4 个以上的连续星号或等号
            string[] markers = { "****", "====" };

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                // 发现标题边界线
                if (markers.Any(m => line.Contains(m)))
                {
                    int headerEnd = i;
                    List<string> headerLines = new List<string>();

                    // 向上/向下探测获取标题真实文字内容
                    while (headerEnd + 1 < lines.Count && !markers.Any(m => lines[headerEnd + 1].Contains(m)))
                    {
                        headerEnd++;
                        string candidate = lines[headerEnd].Trim();
                        if (!string.IsNullOrWhiteSpace(candidate))
                            headerLines.Add(candidate);
                    }

                    // 识别到有效标题文字，说明一个新数据块开始了
                    if (headerLines.Count > 0)
                    {
                        // 1. 结算当前正在收集的旧数据块
                        if (currentBlockLines.Count > 0)
                        {
                            yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                        }

                        // 2. 更新新块的元数据
                        currentTitle = headerLines[0]; // 取标题第一行作为 Key
                        currentBlockLines = new List<string>();
                        startLineNumber = headerEnd + 2;

                        // 3. 跳过标题包围区，继续向下处理数据行
                        i = headerEnd + 1;
                        continue;
                    }
                }

                currentBlockLines.Add(line);
            }

            // 提交文件末尾的最后一个块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}