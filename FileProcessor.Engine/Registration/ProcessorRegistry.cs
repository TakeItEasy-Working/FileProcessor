using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using System.Reflection;

namespace FileProcessor.Engine.Registration
{
    /// <summary>
    /// 处理器注册中心：负责维护块名称与处理器之间的映射关系。
    /// 核心 DLL 通过此类实现“按需分配”解析任务。
    /// </summary>
    public class ProcessorRegistry
    {
        /// <summary>
        /// 核心映射表：Key 为块名，Value 为支持该块的处理器列表
        /// </summary>
        private readonly Dictionary<string, List<IBlockProcessor>> _lookup = new();

        /// <summary>
        /// 全量列表，用于 fallback 兜底匹配
        /// </summary>
        private readonly List<IBlockProcessor> _allProcessors = new();

        /// <summary>
        /// 注册处理器并自动解析其特性标记的块名
        /// </summary>
        public void Register(IBlockProcessor processor)
        {
            _allProcessors.Add(processor);

            // 读取类上定义的所有 BlockProcessorAttribute
            var attrs = processor.GetType().GetCustomAttributes<BlockProcessorAttribute>();
            foreach (var attr in attrs)
            {
                if (!_lookup.ContainsKey(attr.BlockName))
                    _lookup[attr.BlockName] = new List<IBlockProcessor>();

                _lookup[attr.BlockName].Add(processor);
            }
        }

        /// <summary>
        /// 根据块名获取所有匹配的处理器
        /// </summary>
        /// <param name="blockName">切块后得到的 RawBlockName</param>
        public IEnumerable<IBlockProcessor> GetProcessorsForBlock(string blockName)
        {
            // 1. 优先从快速查找字典中匹配
            if (_lookup.TryGetValue(blockName, out var matched))
                return matched.OrderByDescending(p => p.Priority);

            // 2. 如果字典没中，尝试使用处理器自带的 CanProcess 逻辑进行模糊匹配（Fallback）
            return _allProcessors.Where(p => p.CanProcess(blockName))
                                 .OrderByDescending(p => p.Priority);
        }
    }
}