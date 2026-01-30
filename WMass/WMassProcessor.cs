using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Core.Attributes;
using System.Text.RegularExpressions;

namespace WMass.Plugin;

// 注册感兴趣的块标题
[BlockProcessor("各层刚心、偏心率、相邻层侧移刚度比等计算信息")]
public class WMassProcessor : IBlockProcessor
{
    public int Priority => 10;

    // 预编译正则：提升解析性能
    private static readonly Regex FloorRegex = new(@"Floor No\.\s*(?<v>\d+)", RegexOptions.Compiled);
    private static readonly Regex TowerRegex = new(@"Tower No\.\s*(?<v>\d+)", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"(?<key>[A-Za-z0-9&]+)\s*=\s*(?<value>[^ \t]+)", RegexOptions.Compiled);
    private static readonly Regex UnitRegex = new(@"\(.*?\)", RegexOptions.Compiled);

    public bool CanProcess(string blockName) => true; // 匹配所有已注册的块

    public object Process(RawDataBlock block)
    {
        var floorRecords = new List<Dictionary<string, string>>();
        Dictionary<string, string>? currentFloorRow = null;

        foreach (var line in block.RawLines)
        {
            // 1. 识别新楼层块起始 (Floor No.)
            if (line.Contains("Floor No."))
            {
                currentFloorRow = new Dictionary<string, string>();
                floorRecords.Add(currentFloorRow);

                var fMatch = FloorRegex.Match(line);
                var tMatch = TowerRegex.Match(line);

                if (fMatch.Success) currentFloorRow["层号"] = fMatch.Groups["v"].Value;
                if (tMatch.Success) currentFloorRow["塔号"] = tMatch.Groups["v"].Value;
                continue;
            }

            // 2. 解析“Key = Value”格式的数据
            if (currentFloorRow != null)
            {
                var matches = KeyValueRegex.Matches(line);
                foreach (Match m in matches)
                {
                    string key = m.Groups["key"].Value.Trim();
                    string val = m.Groups["value"].Value.Trim();

                    // 剥离单位，如 (m), (t)
                    val = UnitRegex.Replace(val, "");

                    currentFloorRow[key] = val;
                }
            }
        }

        // 返回解析后的结构化数据
        // 这里的匿名对象会被转为 object 存储在 Snapshot 的 DataBlocks 中
        return new
        {
            BlockName = block.BlockName,
            Records = floorRecords,
            Count = floorRecords.Count,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }
}