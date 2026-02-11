using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Reflection;
using System.Text;

namespace FileProcessor.Core.Infrastructure
{
    /// <summary>
    /// 文件解析基类：提供统一的 IO 流处理和编码自动探测。
    /// 这是 DLL 中最核心的逻辑块，确保所有插件遵循相同的读取协议。
    /// </summary>
    public abstract class BaseFileTemplate : IFileTemplate
    {
        /// <summary>
        /// 文件名正则模式（通过反射读取类上的 FileProcessorPluginAttribute）
        /// </summary>
        public virtual string FileNamePattern =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.FileNamePattern ?? string.Empty;

        /// <summary>
        /// 子目录路径（通过反射读取类上的 FileProcessorPluginAttribute）
        /// </summary>
        public virtual string SubDirectory =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.SubDirectory ?? string.Empty;

        /// <summary>
        /// 核心解析流程：执行 IO 读取、编码探测并触发子类的切块逻辑。
        /// </summary>
        /// <param name="filePath">物理文件路径</param>
        /// <returns>拆分后的原始数据块集合</returns>
        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            if (!File.Exists(filePath)) yield break;          

            // 1. 以只读共享模式打开文件，避免锁定正在被写入的工程文件
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // 2. 自动识别编码（默认支持 UTF8 与 GB2312）
            Encoding encoding = DetectEncoding(fs);

            // 3. 读取所有行
            var lines = new List<string>();
            using (var reader = new StreamReader(fs, encoding))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            // 4. 调用子类实现的切块逻辑，并自动注入 Hash
            var blocks = SplitBlocks(lines, filePath);  
            foreach (var block in blocks)
            {                
                yield return block;
            }
        }

        /// <summary>
        /// 子类必须实现：定义如何将文件行集合拆分为独立的逻辑块。
        /// </summary>
        protected abstract IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath);



        private Encoding DetectEncoding(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.");

            long originalPosition = 0;
            if (stream.CanSeek)
                originalPosition = stream.Position;

            try
            {
                const int sampleSize = 8192; // 8KB 更稳
                Span<byte> buffer = stackalloc byte[sampleSize];

                int read = stream.Read(buffer);
                if (stream.CanSeek)
                    stream.Position = originalPosition;

                if (read == 0)
                    return Encoding.UTF8; // 空文件默认 UTF-8

                // =========================
                // 1️⃣ BOM 检测
                // =========================

                // UTF-8 BOM
                if (read >= 3 &&
                    buffer[0] == 0xEF &&
                    buffer[1] == 0xBB &&
                    buffer[2] == 0xBF)
                    return new UTF8Encoding(true);

                // UTF-32 LE BOM
                if (read >= 4 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE &&
                    buffer[2] == 0x00 &&
                    buffer[3] == 0x00)
                    return Encoding.UTF32;

                // UTF-32 BE BOM
                if (read >= 4 &&
                    buffer[0] == 0x00 &&
                    buffer[1] == 0x00 &&
                    buffer[2] == 0xFE &&
                    buffer[3] == 0xFF)
                    return new UTF32Encoding(true, true);

                // UTF-16 LE BOM
                if (read >= 2 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE)
                    return Encoding.Unicode;

                // UTF-16 BE BOM
                if (read >= 2 &&
                    buffer[0] == 0xFE &&
                    buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode;

                // =========================
                // 2️⃣ UTF-8 合法性检测
                // =========================

                if (IsValidUtf8(buffer.Slice(0, read)))
                    return new UTF8Encoding(false);

                // =========================
                // 3️⃣ 回退 GB18030
                // =========================

                return Encoding.GetEncoding("GB18030");
            }
            finally
            {
                if (stream.CanSeek)
                    stream.Position = originalPosition;
            }
        }

        private bool IsValidUtf8(ReadOnlySpan<byte> data)
        {
            int i = 0;
            bool hasMultibyte = false;

            while (i < data.Length)
            {
                byte b = data[i];

                // ASCII
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                int remaining;

                // 2 字节序列
                if (b >= 0xC2 && b <= 0xDF)
                {
                    remaining = 1;
                }
                // 3 字节序列
                else if (b == 0xE0)
                {
                    if (i + 2 >= data.Length ||
                        data[i + 1] < 0xA0 || data[i + 1] > 0xBF ||
                        !IsContinuation(data[i + 2]))
                        return false;
                    i += 3;
                    hasMultibyte = true;
                    continue;
                }
                else if (b >= 0xE1 && b <= 0xEC || b >= 0xEE && b <= 0xEF)
                {
                    remaining = 2;
                }
                else if (b == 0xED) // 避免 UTF-16 surrogate 区
                {
                    if (i + 2 >= data.Length ||
                        data[i + 1] < 0x80 || data[i + 1] > 0x9F ||
                        !IsContinuation(data[i + 2]))
                        return false;
                    i += 3;
                    hasMultibyte = true;
                    continue;
                }
                // 4 字节
                else if (b == 0xF0)
                {
                    if (i + 3 >= data.Length ||
                        data[i + 1] < 0x90 || data[i + 1] > 0xBF ||
                        !IsContinuation(data[i + 2]) ||
                        !IsContinuation(data[i + 3]))
                        return false;
                    i += 4;
                    hasMultibyte = true;
                    continue;
                }
                else if (b >= 0xF1 && b <= 0xF3)
                {
                    remaining = 3;
                }
                else if (b == 0xF4)
                {
                    if (i + 3 >= data.Length ||
                        data[i + 1] < 0x80 || data[i + 1] > 0x8F ||
                        !IsContinuation(data[i + 2]) ||
                        !IsContinuation(data[i + 3]))
                        return false;
                    i += 4;
                    hasMultibyte = true;
                    continue;
                }
                else
                {
                    return false;
                }

                if (i + remaining >= data.Length)
                    return false;

                for (int j = 1; j <= remaining; j++)
                {
                    if (!IsContinuation(data[i + j]))
                        return false;
                }

                i += remaining + 1;
                hasMultibyte = true;
            }

            // 关键策略：
            // 即便全 ASCII 也视为 UTF-8（工业标准）
            return true;
        }

        private bool IsContinuation(byte b)
        {
            return (b & 0xC0) == 0x80;
        }

    }
}