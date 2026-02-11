using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;
using FileProcessor.Core.Contracts;

namespace Mock.Plugin
{
    // 匹配 satter.out 文件
    [FileProcessorPlugin(@"(?i)satter\.out", "设计结果")]
    public class SatterTemplate : BaseFileTemplate
    {
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            // 简单模拟：将整个文件作为一个块
            yield return new RawDataBlock("总荷载统计", lines.ToArray(), 1, filePath);
        }
    }

    [BlockProcessor("总荷载统计")]
    public class SatterProcessor : IBlockProcessor
    {
        public string TargetBlockName => "总荷载统计";
        public int Priority => 1;
        public bool CanProcess(string blockName) => blockName == TargetBlockName;

        public ProcessedResult Process(RawDataBlock block)
        {
            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "建筑总重量汇总",
                Category = "荷载信息",
                Columns = new List<ColumnDefinition> { new("项目", "项目"), new("数值", "数值") },
                Rows = new List<Dictionary<string, string>> {
                    new() { ["项目"] = "恒载总计", ["数值"] = "15000 t" },
                    new() { ["项目"] = "活载总计", ["数值"] = "5000 t" }
                }
            };
        }
    }
}