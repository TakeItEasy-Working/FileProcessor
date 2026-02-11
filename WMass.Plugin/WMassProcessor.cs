using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text.RegularExpressions;

namespace WMass.Plugin
{
    [BlockProcessor("各层刚心、偏心率、相邻层侧移刚度比等计算信息")]
    public class WMassProcessor : IBlockProcessor
    {
        // --- 1. 契约属性 ---
        /// <summary>
        /// 匹配的原始块名称标识
        /// </summary>
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";

        /// <summary>
        /// 处理器优先级
        /// </summary>
        public int Priority => 10;

        /// <summary>
        /// 标准化块 ID：用于 UI 插槽绑定和跨版本历史对比
        /// </summary>
        public string StandardBlockName => "WMass_StiffnessAndCentroid";

        /// <summary>
        /// 默认 UI 分类
        /// </summary>
        public string DefaultCategory => "计算信息";

        /// <summary>
        /// 判定是否可以处理该数据块，只要块名包含“刚心”，就尝试处理
        /// </summary>
        public bool CanProcess(string blockName) => blockName.Contains("刚心");

        // --- 2. 解析逻辑 ---
        /// <summary>
        /// 执行核心解析逻辑
        /// </summary>
        public ProcessedResult Process(RawDataBlock block)
        {
            // 初始化结果：遵循方案 B，利用 init 属性进行一次性赋值
            var result = new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = this.StandardBlockName,
                DisplayName = "各层刚心及刚度比",
                Category = this.DefaultCategory,
                SourceFileName = "wmass.out" // 必须显式指定，用于 SnapshotManager 索引
            };

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

                    // 提取Floor和Tower
                    var floorMatch = Regex.Match(line, @"Floor No\.\s*(?<v>\d+)");
                    var towerMatch = Regex.Match(line, @"Tower No\.\s*(?<v>\d+)");

                    if (floorMatch.Success) currentFloorRow["Floor"] = floorMatch.Groups["v"].Value;
                    if (towerMatch.Success) currentFloorRow["Tower"] = towerMatch.Groups["v"].Value;

                    discoveredKeys.Add("Floor");
                    discoveredKeys.Add("Tower");
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
            // 填充表格定义
            foreach (var key in discoveredKeys)
            {
                result.Columns.Add(new ColumnDefinition(key, key));
            }

            // 填充行数据
            result.Rows.AddRange(floorRecords);

            // 填充扩展元数据
            result.Metadata["StiffnessSummary"] = summaryData;

            return result;
        }
    }
}