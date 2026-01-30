using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text;

namespace FileProcessor.Engine.Runtime
{
    public static class TextParser
    {
        public static IEnumerable<RawDataBlock> Parse(string filePath, IFileTemplate template)
        {
            Console.WriteLine("[通知] 开始文件解析...");

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = DetectEncoding(fs);

            using var reader = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: false);
            string? currentLine;
            string? nextLine = reader.ReadLine();

            string? currentBlockName = null;
            List<string> lineBuffer = new();
            int startLineNumber = 0;
            int currentLineNumber = 0;

            while (nextLine != null)
            {
                currentLine = nextLine;
                nextLine = reader.ReadLine();
                currentLineNumber++;

                // 1. 过滤真正无用的行（空行等）
                if (template.IsIgnorableLine(currentLine)) continue;

                // 2. 识别新块起始
                string? newBlockName = template.IdentifyBlockName(currentLine, nextLine ?? "");

                if (newBlockName != null)
                {
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

        private static Encoding DetectEncoding(FileStream fs)
        {
            // 读取前 4 字节检查 BOM，然后回到流起始位置
            byte[] bom = new byte[4];
            int n = fs.Read(bom, 0, 4);
            fs.Seek(0, SeekOrigin.Begin);

            // UTF-8 BOM EF BB BF
            if (n >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-16 LE BOM FF FE
            if (n >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;

            // UTF-16 BE BOM FE FF
            if (n >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            // UTF-32 LE/BE 等可按需扩展...

            // 无 BOM：大多数中文 Windows 文本是 GBK/GB18030（cp936/54936）
            // 使用 GB18030 能兼容更多中文编码情况
            return Encoding.GetEncoding("GB18030");
        }
    }
}