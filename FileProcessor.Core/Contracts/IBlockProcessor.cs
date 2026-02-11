using FileProcessor.Core.Models;

namespace FileProcessor.Core.Contracts
{
    public interface IBlockProcessor
    {
        // --- 1. 派发机制属性 ---

        /// <summary>
        /// 目标块名：对应特性 BlockProcessorAttribute 中的名称。
        /// 用于维持你目前的快速查找 (Lookup) 机制。
        /// </summary>
        string TargetBlockName { get; }

        /// <summary>
        /// 优先级：当多个处理器竞争同一个块时，优先级高的胜出。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 模糊匹配逻辑：用于双轨制中的 fallback 匹配。
        /// </summary>
        bool CanProcess(string blockName);

        // --- 2. 数据对齐属性 ---

        /// <summary>
        /// 标准化块标识（如 StandardBlock_Stiffness）。
        /// 无论 YJK 还是 PKPM，只要性质相同，此 ID 必须一致。
        /// </summary>
        string StandardBlockName { get; }

        /// <summary>
        /// 业务分类（如“位移结果”），用于 UI 分组。
        /// </summary>
        string DefaultCategory { get; }

        // --- 3. 核心执行方法 ---

        /// <summary>
        /// 执行解析逻辑。
        /// </summary>
        ProcessedResult Process(RawDataBlock block);
    }
}