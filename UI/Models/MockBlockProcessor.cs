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
            var result = new ProcessedResult
            {
                BlockName = block.BlockName,
                VersionId = "V1.0",
                // 修正：使用 List<ColumnDefinition> 而不是 Dictionary
                Columns = new List<ColumnDefinition> {
            new ColumnDefinition (  "ID",    "编号" ),
            new ColumnDefinition (  "Val",   "数值" ),
            new ColumnDefinition (  "Time",  "生成时间" )
        },
                Rows = new List<Dictionary<string, string>>()
            };

            for (int i = 1; i <= 3; i++)
            {
                result.Rows.Add(new Dictionary<string, string> {
            { "ID", i.ToString() },
            { "Val", new Random().Next(100, 999).ToString() },
            { "Time", DateTime.Now.ToString("HH:mm:ss") }
        });
            }
            return result;
        }
    }
}