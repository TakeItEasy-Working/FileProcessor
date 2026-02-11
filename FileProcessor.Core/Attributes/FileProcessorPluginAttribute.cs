using System;

namespace FileProcessor.Core.Attributes
{
    /// <summary>
    /// 文件处理器插件特性：用于标记 IFileTemplate 的实现类。
    /// 核心 DLL 将通过此特性识别插件及其监控的文件规则。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FileProcessorPluginAttribute : Attribute
    {
        /// <summary>
        /// 获取匹配该文件的正则表达式模式（如 @"(?i)wmass\.out"）
        /// </summary>
        public string FileNamePattern { get; }

        /// <summary>
        /// 获取该文件通常所在的相对子目录（如 "设计结果"）
        /// </summary>
        public string SubDirectory { get; }

        /// <summary>
        /// 初始化文件处理器插件特性
        /// </summary>
        /// <param name="fileNamePattern">文件名正则匹配模式</param>
        /// <param name="subDirectory">所属子目录，默认为空</param>
        public FileProcessorPluginAttribute(string fileNamePattern, string subDirectory = "")
        {
            FileNamePattern = fileNamePattern;
            SubDirectory = subDirectory;
        }
    }
}