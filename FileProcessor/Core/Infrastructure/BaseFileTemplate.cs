using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace FileProcessor.Core.Infrastructure
{
    /// <summary>
    /// 文件解析基类：提供统一的 IO 流处理和编码检测
    /// </summary>
    public abstract class BaseFileTemplate : IFileTemplate
    {
        public abstract string FileNamePattern { get; }

        public abstract string SubDirectory { get; }

        /// <summary>
        /// 基础设施层：负责安全读取文件并触发子类拆分逻辑
        /// </summary>
        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            if (!File.Exists(filePath)) yield break;

            // 1. 计算文件指纹 (MD5)
            string fileHash = CalculateHash(filePath);

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
                block.FileHash = fileHash;
                yield return block;
            }
        }

        /// <summary>
        /// 子类需实现的具体拆分算法
        /// </summary>
        protected abstract IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath);

        private Encoding DetectEncoding(FileStream fs)
        {
            byte[] buffer = new byte[4096]; // 读取前 4KB 进行特征分析
            int readCount = fs.Read(buffer, 0, buffer.Length);
            fs.Position = 0; // 必须回位

            // 1. 优先识别有标准 BOM 的情况
            if (readCount >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) return Encoding.UTF8;
            if (readCount >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE) return Encoding.Unicode;

            // 2. 启发式探测：检查是否为无 BOM 的 UTF-8
            if (IsUtf8(buffer, readCount))
            {
                return Encoding.UTF8;
            }

            // 3. 最后的兜底：对于国产软件，极大概率是 GB2312 (936)
            // 确保 App.xaml.cs 已经注册了 CodePagesEncodingProvider
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch
            {
                // 极致兼容：如果 931 失败，回退到系统默认
                return Encoding.Default;
            }
        }

        /// <summary>
        /// 检查字节数组是否符合 UTF-8 编码规则
        /// </summary>
        private bool IsUtf8(byte[] buffer, int length)
        {
            int i = 0;
            bool hasMultiByte = false;
            while (i < length)
            {
                if (buffer[i] <= 0x7F) { i++; continue; } // ASCII 范围

                // 检查多字节序列特征
                int count = 0;
                if (buffer[i] >= 0xC2 && buffer[i] <= 0xDF) count = 2;
                else if (buffer[i] >= 0xE0 && buffer[i] <= 0xEF) count = 3;
                else if (buffer[i] >= 0xF0 && buffer[i] <= 0xF4) count = 4;
                else return false; // 非法起始字节

                if (i + count > length) break;
                for (int j = 1; j < count; j++)
                {
                    if (buffer[i + j] < 0x80 || buffer[i + j] > 0xBF) return false;
                }
                hasMultiByte = true;
                i += count;
            }
            return hasMultiByte; // 如果有正确的多字节，极有可能是 UTF-8
        }

        private string CalculateHash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}