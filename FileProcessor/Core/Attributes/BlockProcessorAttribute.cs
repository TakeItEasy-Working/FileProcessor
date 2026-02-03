using System;

namespace FileProcessor.Core.Attributes
{
    /// <summary>
    /// 数据块处理器特性：用于标记 IBlockProcessor 的实现类。
    /// 核心 DLL 将根据此特性自动将切分后的 RawDataBlock 分发给对应的处理器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class BlockProcessorAttribute : Attribute
    {
        /// <summary>
        /// 该处理器支持的块标题名称或关键标识
        /// </summary>
        public string BlockName { get; }

        /// <summary>
        /// 优先级：当多个处理器匹配同一个块时，值越大（越高）越优先处理
        /// </summary>
        public int Priority { get; init; } = 0;

        /// <summary>
        /// 初始化数据块处理器特性
        /// </summary>
        /// <param name="blockName">支持的数据块名称</param>
        public BlockProcessorAttribute(string blockName)
        {
            BlockName = blockName;
        }
    }
}