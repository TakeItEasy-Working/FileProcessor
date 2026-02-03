using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Core.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FileProcessor.Core.Infrastructure
{
    /// <summary>
    /// 文件解析基类：提供统一的 IO 流处理、文件指纹计算和编码自动探测。
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

            // 1. 计算文件指纹，确保后续数据追踪的唯一性
            string fileHash = CalculateHash(filePath);

            // 2. 以只读共享模式打开文件，避免锁定正在被写入的工程文件
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // 3. 自动识别编码（默认支持 UTF8 与 GB2312）
            Encoding encoding = DetectEncoding(fs);

            // 4. 读取所有行
            var lines = new List<string>();
            using (var reader = new StreamReader(fs, encoding))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            // 5. 调用子类实现的切块逻辑，并自动注入 Hash
            var blocks = SplitBlocks(lines, filePath);
            foreach (var block in blocks)
            {
                block.FileHash = fileHash; // 核心封版要求：在此处统一注入 Hash
                yield return block;
            }
        }

        /// <summary>
        /// 子类必须实现：定义如何将文件行集合拆分为独立的逻辑块。
        /// </summary>
        protected abstract IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath);

        /// <summary>
        /// 计算文件的 SHA256 哈希值
        /// </summary>
        private string CalculateHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hashBytes = sha.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }

        /// <summary>
        /// 启发式探测文件编码
        /// </summary>
        private Encoding DetectEncoding(Stream stream)
        {
            byte[] bom = new byte[4];
            stream.Read(bom, 0, 4);
            stream.Seek(0, SeekOrigin.Begin);

            // 1. BOM 检查
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode;

            // 2. 默认使用简体中文 GB2312 (CodePage 936)
            return Encoding.GetEncoding(936);
        }
    }
}