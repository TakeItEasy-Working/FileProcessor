using FileProcessor.Core.Models;

namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 插件标准化接口
    /// </summary>
    public interface IFileTemplate
    {
        // 匹配的文件名正则模式
        string FileNamePattern { get; }

        // 解析入口：负责从路径直接输出拆分后的原始数据块
        IEnumerable<RawDataBlock> Parse(string filePath);
    }
}