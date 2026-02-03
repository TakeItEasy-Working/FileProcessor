using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WMass.Plugin
{
    public class WMassProcessor : IBlockProcessor
    {
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
        public int Priority => 10;

        // 只要块名包含“刚心”，就尝试处理
        public bool CanProcess(string blockName) => blockName.Contains("刚心");

        public ProcessedResult Process(RawDataBlock block)
        {
            var floorRecords = new List<Dictionary<string, string>>();
            var discoveredKeys = new HashSet<string>(); // 使用 HashSet 记录所有出现过的 Key
            Dictionary<string, string>? currentFloorRow = null;
            var summaryData = new List<Dictionary<string, string>>();

            foreach (var rawLine in block.Lines)
            {
                string line = rawLine.Trim();

                // 1. 跳过分界线和空行
                if (string.IsNullOrWhiteSpace(line) || line.Contains("----"))
                    continue;

                // 2. 识别新楼层块起始 (算法核心 A)
                if (line.Contains("Floor No."))
                {
                    currentFloorRow = new Dictionary<string, string>();
                    floorRecords.Add(currentFloorRow);

                    // 提取层号和塔号
                    var floorMatch = Regex.Match(line, @"Floor No\.\s*(?<v>\d+)");
                    var towerMatch = Regex.Match(line, @"Tower No\.\s*(?<v>\d+)");

                    if (floorMatch.Success) currentFloorRow["层号"] = floorMatch.Groups["v"].Value;
                    if (towerMatch.Success) currentFloorRow["塔号"] = towerMatch.Groups["v"].Value;

                    discoveredKeys.Add("层号");
                    discoveredKeys.Add("塔号");
                    continue;
                }

                // 3. 解析“标题=数据”格式的行 (算法核心 B)
                if (currentFloorRow != null)
                {
                    // 匹配模式：Key [空格或等号] Value
                    var matches = Regex.Matches(line, @"(?<key>[A-Za-z0-9&]+)\s*=\s*(?<value>[^ \t]+)");
                    foreach (Match m in matches)
                    {
                        string key = m.Groups["key"].Value.Trim();
                        string val = m.Groups["value"].Value.Trim();

                        // 去除数值中的单位，如 (m), (t)
                        val = Regex.Replace(val, @"\(.*?\)", "");

                        currentFloorRow[key] = val;
                        discoveredKeys.Add(key);
                    }
                }

                // 4. 处理特殊总结行
                if (line.Contains("最小刚度比"))
                {
                    var minMatch = Regex.Match(line, @"(?<key>.*方向最小刚度比):?\s*(?<val>[0-9\.]+)");
                    if (minMatch.Success)
                    {
                        summaryData.Add(new Dictionary<string, string> {
                            { "描述", minMatch.Groups["key"].Value.Trim() },
                            { "数值", minMatch.Groups["val"].Value.Trim() }
                        });
                    }
                }
            }

            // 5. 封装返回结果
            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "刚心及刚度比详情",
                Category = "计算信息",
                Rows = floorRecords,
                // 根据发现的 Key 动态生成列定义，保证 UI 有表头
                Columns = discoveredKeys.Select(k => new ColumnDefinition(k, k)).ToList(),
                Metadata = new Dictionary<string, object>
                {
                    { "OriginHash", block.FileHash },
                    { "StiffnessSummary", summaryData }
                }
            };
        }
    }
}