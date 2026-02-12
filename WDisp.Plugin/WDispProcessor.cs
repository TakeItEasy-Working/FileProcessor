using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text.RegularExpressions;

namespace WDisp.Plugin
{
    /// <summary>
    /// 位移输出文件（wdisp.out）的高级解析器。
    /// 采用基于“前导空格缩进”的物理行识别算法，解决折叠表格中的主键（Floor）缺失与索引位移问题。
    /// </summary>
    [BlockProcessor("位移输出")]
    public class WDispProcessor : IBlockProcessor
    {
        public string TargetBlockName => "位移输出";
        public int Priority => 10;

        /// <summary>
        /// 标准化 ID：对应插槽配置，不同工况共享此 ID 但通过版本或 Metadata 区分
        /// </summary>
        public string StandardBlockName => "WDisp_DisplacementData";

        /// <summary>
        /// 默认 UI 分类
        /// </summary>
        public string DefaultCategory => "位移信息";

        /// <summary>
        /// 判定该块是否属于位移结果类数据
        /// </summary>
        public bool CanProcess(string blockName) => blockName.Contains("位移");

        public ProcessedResult Process(RawDataBlock block)
        {
            var rows = new List<Dictionary<string, string>>();
            var allColumns = new HashSet<string> { "Floor", "Tower" };

            // 1. 预处理：过滤干扰行并分析缩进规律
            var lines = block.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("---") && !l.Contains("***"))
                .ToList();

            if (lines.Count == 0) return null;

            // 2. 探测表头步长 (Stride) 与 物理表头映射
            int stride = 0;
            var headerLines = new List<string>();
            while (stride < lines.Count && !Regex.IsMatch(lines[stride].Trim(), @"^\d+"))
            {
                headerLines.Add(lines[stride]);
                stride++;
            }

            if (stride == 0 || stride >= lines.Count) return null;

            // 构建逻辑表头：headerGroups[物理行索引] = 该物理行对应的列名列表
            var headerGroups = BuildHeaderGroups(headerLines);
            foreach (var group in headerGroups)
                foreach (var col in group) allColumns.Add(col);

            // 3. 核心解析循环：以 Stride 为步长处理逻辑行组
            string lastFloor = "1";
            string lastTower = "1";

            for (int i = stride; i + stride <= lines.Count; i += stride)
            {
                var logicalRow = new Dictionary<string, string>();

                for (int s = 0; s < stride; s++)
                {
                    string currentLine = lines[i + s];
                    int indent = GetIndentCount(currentLine); // 获取当前物理行前导空格数

                    // 利用 Regex.Split 进行初步切分
                    var parts = Regex.Split(currentLine.Trim(), @"\s+").ToList();
                    if (parts.Count == 0) continue;

                    // A. 处理逻辑行起始行 (通常缩进较小)
                    if (s == 0)
                    {
                        // 根据规律：不带Floor的续行缩进更大。
                        // 判定逻辑：如果缩进较小且首位是数字，则是新Floor
                        if (indent < 8 && Regex.IsMatch(parts[0], @"^\d+$"))
                        {
                            lastFloor = parts[0];
                            // 如果第二位也是短数字，更新Tower
                            if (parts.Count > 1 && parts[1].Length < 4 && Regex.IsMatch(parts[1], @"^\d+$"))
                            {
                                lastTower = parts[1];
                            }
                        }

                        // 填充主键
                        logicalRow["Floor"] = lastFloor;
                        logicalRow["Tower"] = lastTower;

                        // 确定当前物理行的数据起始映射偏移
                        // 如果当前行开头没有Floor（缩进大或非数字），数据索引需要向右偏移映射
                        int headerOffset = (indent > 8) ? 2 : 0;
                        MapPartsToHeaders(parts, headerOffset, headerGroups[s], logicalRow);
                    }
                    else
                    {
                        // B. 处理折叠续行 (s > 0)
                        // 续行通常不含Floor和Tower，数据直接从对应表头的第三列(Index 2)开始
                        MapPartsToHeaders(parts, 2, headerGroups[s], logicalRow);
                    }
                }
                rows.Add(logicalRow);
            }

            return new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = StandardBlockName,
                Rows = rows,
                Category = "位移结果",
                Columns = allColumns.Select(c => new ColumnDefinition(c, c)).ToList()
            };
        }

        /// <summary>
        /// 获取行首前导空格数量
        /// </summary>
        private int GetIndentCount(string line)
        {
            int count = 0;
            foreach (char c in line)
            {
                if (c == ' ') count++;
                else break;
            }
            return count;
        }

        /// <summary>
        /// 将多行表头解析为物理行对应的映射组
        /// </summary>
        private List<List<string>> BuildHeaderGroups(List<string> headerLines)
        {
            var groups = new List<List<string>>();
            foreach (var line in headerLines)
            {
                var parts = Regex.Split(line.Trim(), @"\s+");
                groups.Add(parts.Select(p => MapColumnName(p)).ToList());
            }
            return groups;
        }

        /// <summary>
        /// 执行点对点的数据绑定与特殊转换
        /// </summary>
        /// <param name="parts">物理行拆分后的数值</param>
        /// <param name="headerStartIdx">该物理行对应的表头起始索引（解决左移问题）</param>
        /// <param name="currentHeaders">当前物理行的列名定义</param>
        /// <param name="row">目标字典</param>
        private void MapPartsToHeaders(List<string> parts, int headerStartIdx, List<string> currentHeaders, Dictionary<string, string> row)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                int logicalIdx = i + headerStartIdx;
                if (logicalIdx < currentHeaders.Count)
                {
                    string colName = currentHeaders[logicalIdx];
                    string val = parts[i];
                    row[colName] = val;

                    // 触发位移角转换逻辑
                    if ((colName.Contains("位移角") || colName.Contains("/h")) && val.Contains("/"))
                    {
                        var driftParts = val.Split('/');
                        if (driftParts.Length == 2 && double.TryParse(driftParts[1], out double d) && d != 0)
                        {
                            row[colName + "_数值"] = (1.0 / d).ToString("F6");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 翻译原始列名为标准的业务中文名，保留方向感
        /// </summary>
        private string MapColumnName(string raw)
        {
            if (raw.Contains("Max-Dx/h")) return "最大层间位移角(X)";
            if (raw.Contains("Max-Dy/h")) return "最大层间位移角(Y)";
            if (raw.Contains("Max-(X)")) return "最大位移(X)";
            if (raw.Contains("Max-(Y)")) return "最大位移(Y)";
            if (raw.Contains("Ratio-Dx")) return "层间位移比(X)";
            if (raw.Contains("Ratio-Dy")) return "层间位移比(Y)";
            return raw;
        }
    }
}