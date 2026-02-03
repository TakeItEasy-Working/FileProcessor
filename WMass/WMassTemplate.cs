using FileProcessor.Core.Contracts;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WMass.Plugin
{
    public class WMassTemplate : BaseFileTemplate
    {
        // 匹配模式：忽略大小写更稳健
        public override string FileNamePattern => @"wmass.out";

        // YJK 输出目录
        public override string SubDirectory => "设计结果";

        /// <summary>
        /// 基类 Parse 完成 IO 和编码识别后，会将行列表传回这里
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