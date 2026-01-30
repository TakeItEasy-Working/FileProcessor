using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text;

namespace FileProcessor.Core.Infrastructure
{
    /// <summary>
    /// 文件解析基类：提供统一的 IO 流处理和编码检测
    /// </summary>
    public abstract class BaseFileTemplate : IFileTemplate
    {
        public abstract string FileNamePattern { get; }

        /// <summary>
        /// 基础设施层：负责安全读取文件并触发子类拆分逻辑
        /// </summary>
        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            if (!File.Exists(filePath)) yield break;

            // 1. 使用 FileShare.ReadWrite 兼容 Watcher 和各种编辑器
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // 2. 自动识别编码（支持 UTF8/Unicode/GB2312）
            Encoding encoding = DetectEncoding(fs);

            // 3. 将文件内容读入内存（防止流关闭异常）
            var lines = new List<string>();
            using (var reader = new StreamReader(fs, encoding))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            // 4. 调用具体实现的拆分算法
            foreach (var block in SplitBlocks(lines, filePath))
            {
                yield return block;
            }
        }

        /// <summary>
        /// 子类需实现的具体拆分算法
        /// </summary>
        protected abstract IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath);

        private Encoding DetectEncoding(FileStream fs)
        {
            byte[] bom = new byte[4];
            int n = fs.Read(bom, 0, 4);
            fs.Seek(0, SeekOrigin.Begin); // 关键：回位

            if (n >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
            if (n >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;

            // 默认 GB2312 (解决 YJK 等国产软件输出的乱码问题)
            return Encoding.GetEncoding("GB2312");
        }
    }
}