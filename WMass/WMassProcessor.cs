using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WMass.Plugin
{
    /// <summary>
    /// 专门处理“各层刚心、偏心率、相邻层侧移刚度比等计算信息”块的处理器
    /// </summary>
    public class WMassProcessor : IBlockProcessor
    {
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
        public int Priority => 10;

        public bool CanProcess(string blockName) => blockName.Contains("刚心");

        public ProcessedResult Process(RawDataBlock block)
        {
            var floorRecords = new List<Dictionary<string, string>>();
            var discoveredKeys = new List<string>(); // 记录发现的所有 Key 顺序
            Dictionary<string, string>? currentFloorRow = null;

            // 为了兼容架构，我们也需要记录“刚度总结”类的特殊行
            // 但在 ProcessedResult 中我们优先呈现主表格
            var summaryData = new List<Dictionary<string, string>>();

            foreach (var rawLine in block.Lines)
            {
                string line = rawLine.Trim();

                // 1. 跳过分界线和空行
                if (string.IsNullOrWhiteSpace(line) || line.Contains("----"))
                    continue;

                // 2. 处理注释行 (逻辑保留：存入 Metadata 或丢弃)
                if (line.Contains(":") && !line.Contains("="))
                {
                    // 此处可扩展：将注释存入 ProcessedResult.Metadata
                    continue;
                }

                // 3. 识别新楼层块起始 (Floor No.)
                if (line.Contains("Floor No."))
                {
                    currentFloorRow = new Dictionary<string, string>();
                    floorRecords.Add(currentFloorRow);

                    // 提取层号和塔号
                    var floorMatch = Regex.Match(line, @"Floor No\.\s*(?<v>\d+)");
                    var towerMatch = Regex.Match(line, @"Tower No\.\s*(?<v>\d+)");

                    void AddWithKey(string key, string val)
                    {
                        currentFloorRow![key] = val;
                        if (!discoveredKeys.Contains(key)) discoveredKeys.Add(key);
                    }

                    if (floorMatch.Success) AddWithKey("层号", floorMatch.Groups["v"].Value);
                    if (towerMatch.Success) AddWithKey("塔号", towerMatch.Groups["v"].Value);
                    continue;
                }

                // 4. 解析“标题=数据”格式的行
                if (currentFloorRow != null)
                {
                    // 匹配模式：字母数字组合 + 等号 + 数字/科学计数法/括号后缀
                    var matches = Regex.Matches(line, @"(?<key>[A-Za-z0-9&]+)\s*=\s*(?<value>[^ \t]+)");
                    foreach (Match m in matches)
                    {
                        string key = m.Groups["key"].Value.Trim();
                        string val = m.Groups["value"].Value.Trim();

                        // 核心：去除数值中的单位，如 (m), (t), (Degree)
                        val = Regex.Replace(val, @"\(.*?\)", "");

                        currentFloorRow[key] = val;
                        if (!discoveredKeys.Contains(key)) discoveredKeys.Add(key);
                    }

                    // 5. 特殊处理“最小刚度比” (针对这一行可能出现在块中间的情况)
                    if (line.Contains("最小刚度比"))
                    {
                        var minMatch = Regex.Match(line, @"(?<key>.*方向最小刚度比):\s*(?<val>[0-9\.]+)");
                        if (minMatch.Success)
                        {
                            summaryData.Add(new Dictionary<string, string> {
                                { "描述", minMatch.Groups["key"].Value.Trim() },
                                { "数值", minMatch.Groups["val"].Value.Trim() }
                            });
                        }
                    }
                }
            }

            // 构造返回对象
            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "楼层侧移刚度比(深度解析)",
                Category = "计算信息",
                Rows = floorRecords,
                // 将动态发现的所有 Key 生成列定义
                Columns = discoveredKeys.Select(k => new ColumnDefinition(k, k)).ToList(),
                // 将“刚度总结”数据放入 Metadata，WPF 可根据需要从 Metadata 读取并二次展示
                Metadata = {
                    { "OriginHash", block.FileHash },
                    { "StiffnessSummary", summaryData }
                }
            };
        }
    }
}