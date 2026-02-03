using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WMass.Plugin
{
    /// <summary>
    /// WMass 结果文件解析插件。
    /// 通过特性告知核心 DLL：监控 wmass.out，且它位于“设计结果”子目录。
    /// </summary>
    [FileProcessorPlugin(@"(?i)wmass\.out", "设计结果")]
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

            string[] markers = { "****", "====" };

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (markers.Any(m => line.Contains(m)))
                {
                    int headerEnd = i;
                    List<string> headerLines = new List<string>();

                    while (headerEnd + 1 < lines.Count && !markers.Any(m => lines[headerEnd + 1].Contains(m)))
                    {
                        headerEnd++;
                        string candidate = lines[headerEnd].Trim();
                        if (!string.IsNullOrWhiteSpace(candidate))
                            headerLines.Add(candidate);
                    }

                    if (headerLines.Count > 0)
                    {
                        if (currentBlockLines.Count > 0)
                        {
                            yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                        }

                        currentTitle = headerLines[0];
                        currentBlockLines = new List<string>();
                        startLineNumber = headerEnd + 2;
                        i = headerEnd + 1;
                        continue;
                    }
                }
                currentBlockLines.Add(line);
            }

            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}