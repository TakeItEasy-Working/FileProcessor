using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Generic;
using System.IO;

namespace FileProcessor.Engine.Runtime
{
    public static class TextParser
    {
        public static IEnumerable<RawDataBlock> Parse(string filePath, IFileTemplate template)
        {
            Console.WriteLine("parsing...");
            using var reader = new StreamReader(filePath);
            string? currentLine;
            string? nextLine = reader.ReadLine();

            string? currentBlockName = null;
            List<string> lineBuffer = new();
            int startLineNumber = 0;
            int currentLineNumber = 0;

            while (nextLine != null)
            {
                Console.WriteLine(nextLine);
                currentLine = nextLine;
                nextLine = reader.ReadLine();
                currentLineNumber++;

                // 1. 过滤真正无用的行（空行等）
                if (template.IsIgnorableLine(currentLine)) continue;

                // 2. 识别新块起始
                string? newBlockName = template.IdentifyBlockName(currentLine, nextLine ?? "");

                if (newBlockName != null)
                {
                    Console.WriteLine(newBlockName);
                    // 提交旧块
                    if (currentBlockName != null && lineBuffer.Count > 0)
                    {
                        yield return new RawDataBlock(currentBlockName, lineBuffer.ToArray(), startLineNumber, filePath);
                    }

                    // 开启新块
                    currentBlockName = newBlockName;
                    startLineNumber = currentLineNumber;
                    lineBuffer.Clear();

                    // 【核心逻辑调整】
                    // 因为 currentLine 是 "****" 这种标题触发线，它本身不是数据，不要加入 lineBuffer
                    // 我们直接跳过 currentLine，让下一次循环处理 nextLine (即标题行)
                    continue;
                }

                // 3. 收集内容
                if (currentBlockName != null)
                {
                    lineBuffer.Add(currentLine);
                }
            }

            // 提交最后一个块
            if (currentBlockName != null && lineBuffer.Count > 0)
            {
                yield return new RawDataBlock(currentBlockName, lineBuffer.ToArray(), startLineNumber, filePath);
            }
        }
    }
}