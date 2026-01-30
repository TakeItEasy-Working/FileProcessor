using FileProcessor.Core.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Engine.Registration
{
    public class ProcessorRegistry
    {
        // Key: BlockName, Value: 排序后的处理器列表
        private readonly ConcurrentDictionary<string, List<IBlockProcessor>> _processors = new();

        public void Register(string blockName, IBlockProcessor processor)
        {
            var list = _processors.GetOrAdd(blockName, _ => new List<IBlockProcessor>());
            list.Add(processor);
            // 按优先级降序排序，确保匹配时先取到 Priority 高的
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public IBlockProcessor? FindProcessor(string blockName)
        {
            if (_processors.TryGetValue(blockName, out var list))
            {
                return list.FirstOrDefault();
            }

            // 如果没有精确匹配，可以遍历列表通过 CanProcess(blockName) 进行模糊匹配
            return _processors.Values
                .SelectMany(l => l)
                .FirstOrDefault(p => p.CanProcess(blockName));
        }
    }
}
