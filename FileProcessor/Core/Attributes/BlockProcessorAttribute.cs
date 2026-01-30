using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BlockProcessorAttribute(string blockName) : Attribute
    {
        public string BlockName { get; } = blockName;

        // 可以扩展更多元数据
        public int Priority { get; init; } = 0;
    }
}
