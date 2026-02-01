using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;

namespace ConProjectMonitor
{
    public class MockBlockProcessor : IBlockProcessor
    {
        // 简单处理：只要块名包含 "Block" 就处理
        public string TargetBlockName => "MockBlock";

        public bool CanProcess(string blockName) => blockName.Contains("Block");

        public int Priority => 1;

        public ProcessedResult Process(RawDataBlock block)
        {
            // 模拟将行数据转换为表格行
            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Key", "Val1" }, { "Raw", block.Lines.FirstOrDefault() ?? "" } }
            };

            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "模拟显示块",
                Category = "测试分类",
                Rows = rows,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition("Key", "键"),
                    new ColumnDefinition("Raw", "原始行数据")
                }
            };
        }
    }
}