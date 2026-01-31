using FileProcessor.Core.Contracts;
using System.Collections.Concurrent;

namespace FileProcessor.Engine.Registration
{
    public class ProcessorRegistry
    {
        // 改为接口IBlockProcessor
        private readonly List<IBlockProcessor> _processors = new();

        public void Register(IBlockProcessor processor) => _processors.Add(processor);

        /// <summary>
        /// 根据块名和 CanProcess 逻辑筛选所有匹配的处理器
        /// </summary>
        public IEnumerable<IBlockProcessor> GetProcessorsForBlock(string blockName)
        {
            return _processors.Where(p =>
                p.TargetBlockName == blockName || p.CanProcess(blockName));
        }
    }
}
