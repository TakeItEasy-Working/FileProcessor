using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WMass.Plugin
{
    /// <summary>
    /// WMass 结果文件解析插件。
    /// 通过特性告知核心 DLL：监控 wmass.out，且它位于“设计结果”子目录。
    /// </summary>
    [FileProcessorPlugin(@"wmass.out", "设计结果")]
    public class WMassTemplate : BaseFileTemplate
    {
        /// <summary>
        /// 仅需实现核心的切块逻辑，IO 和编码由核心 DLL 处理
        /// </summary>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            string[] majorMarkers = { "****", "====" };

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool isMarker = majorMarkers.Any(m => line.Contains(m));

                if (isMarker)
                {
                    // 发现潜在的标题区域开始，向下寻找闭合的 Marker
                    int closingIndex = -1;
                    // 设定一个合理的查找范围（例如往下找 20 行），避免无限查找
                    for (int j = i + 1; j < lines.Count && j < i + 20; j++)
                    {
                        if (majorMarkers.Any(m => lines[j].Contains(m)))
                        {
                            closingIndex = j;
                            break;
                        }
                    }

                    // 如果找到了闭合 Marker，且中间有内容
                    if (closingIndex > i + 1)
                    {
                        // 1. 提取标题区域的所有行（排除上下装饰线）
                        var headerRegion = new List<string>();
                        for (int k = i + 1; k < closingIndex; k++)
                        {
                            string hLine = lines[k].Trim();
                            if (!string.IsNullOrWhiteSpace(hLine))
                            {
                                headerRegion.Add(hLine);
                            }
                        }

                        if (headerRegion.Count > 0)
                        {
                            // A. 结算上一个块
                            if (currentBlockLines.Count > 0)
                            {
                                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                            }

                            // B. 启动新块
                            // 第一行作为标准 RawBlockName
                            currentTitle = headerRegion[0];

                            // C. 核心逻辑修改：将标题区域剩余的行（摘要信息）下沉到数据行中
                            currentBlockLines = new List<string>();
                            if (headerRegion.Count > 1)
                            {
                                // Skip(1) 把除了第一行标题外的其他行都加进去
                                currentBlockLines.AddRange(headerRegion.Skip(1));
                            }

                            // D. 指针跳转到闭合 Marker 处
                            startLineNumber = closingIndex + 1;
                            i = closingIndex;
                            continue;
                        }
                    }
                }

                // 非 Marker 行（或未匹配成对 Marker 的行），作为普通数据收集
                currentBlockLines.Add(line);
            }

            // 结算文件末尾的最后一块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}