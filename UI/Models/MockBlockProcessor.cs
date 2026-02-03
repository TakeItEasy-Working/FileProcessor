using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System;
using System.Collections.Generic;

namespace UI.Models // 请确保命名空间与你的项目路径一致
{
    public class MockBlockProcessor : IBlockProcessor
    {
        // 1. 显式实现接口成员：TargetBlockName
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";

        // 2. 显式实现接口成员：Priority
        public int Priority => 10;

        // 3. 匹配逻辑
        public bool CanProcess(string blockName) =>
            blockName == TargetBlockName || blockName == "楼层位移总结";

        public ProcessedResult Process(RawDataBlock block)
        {
            // 注意：ProcessedResult 的属性多为 init，适合用对象初始化器
            var result = new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = block.BlockName == TargetBlockName ? "刚心与偏心率" : "楼层位移",
                Category = "模拟数据"
            };

            // 4. 定义列元数据
            result.Columns.Add(new ColumnDefinition("Floor", "层号", IsNumeric: true));
            result.Columns.Add(new ColumnDefinition("Value", "模拟数值", IsNumeric: true));

            // 5. 生成模拟行数据
            var random = new Random();
            for (int i = 1; i <= 5; i++)
            {
                // 必须显式声明为 Dictionary<string, string> 以匹配 ProcessedResult.Rows
                var row = new Dictionary<string, string>();
                row.Add("Floor", i.ToString());
                row.Add("Value", (random.Next(100, 500) / 10.0).ToString("F2"));

                result.Rows.Add(row);
            }

            return result;
        }
    }
}