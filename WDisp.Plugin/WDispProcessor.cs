using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WDisp.Plugin
{
    /// <summary>
    /// 位移输出文件（wdisp.out）的高级解析器。
    /// 采用“步长(Stride)逻辑行探测”结合“列数推断状态机”的混合算法。
    /// 彻底消灭缩进魔法数字，完美解决折叠表格中的主键（Floor）缺失、内部数据断裂及对齐偏移问题。
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

        /// <summary>
        /// 预编译正则表达式：用于清洗 "1/  999." 中 "/" 后面的多余空格，
        /// 防止由于固定宽度排版内部自带空格导致列数切割错位。
        /// </summary>
        private readonly Regex spaceFixer = new Regex(@"(1/)\s+", RegexOptions.Compiled);

        /// <summary>
        /// 核心处理方法：将原始文本块转化为结构化的字典集合
        /// </summary>
        public ProcessedResult Process(RawDataBlock block)
        {
            var rows = new List<Dictionary<string, string>>();
            var allColumns = new HashSet<string> { "Floor", "Tower" };

            // 1. 预处理：过滤空白行和无意义的装饰性分割线
            var lines = block.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("---") && !l.Contains("***") && !l.Contains("==="))
                .Select(l => l.Trim())
                .ToList();

            if (lines.Count == 0) return null;

            // 2. 探测表头步长 (Stride) 与提取物理表头
            // 逻辑：连续向下扫描，直到遇到第一行以数字开头的数据行，之前的行全部认为是表头
            int stride = 0;
            var headerLines = new List<string>();
            while (stride < lines.Count && !Regex.IsMatch(lines[stride], @"^\d+"))
            {
                headerLines.Add(lines[stride]);
                stride++;
            }

            if (stride == 0 || stride >= lines.Count) return null;

            // 构建逻辑表头：headerGroups[物理行索引] = 该物理行对应的列名列表
            var headerGroups = BuildHeaderGroups(headerLines);
            foreach (var group in headerGroups)
            {
                foreach (var col in group)
                {
                    allColumns.Add(col);
                }
            }

            // 3. 核心解析循环：以 Stride 为步长处理物理行组（形成一个完整的逻辑行）
            string lastFloor = "1";
            string lastTower = "1";

            for (int i = stride; i + stride <= lines.Count; i += stride)
            {
                var logicalRow = new Dictionary<string, string>();

                for (int s = 0; s < stride; s++)
                {
                    string currentLine = lines[i + s];

                    // 【核心清洗】修复 "1/   999." 的数据断裂问题
                    currentLine = spaceFixer.Replace(currentLine, "$1");

                    // 利用 Regex.Split 按空白字符进行切分
                    var parts = Regex.Split(currentLine, @"\s+").Where(p => !string.IsNullOrEmpty(p)).ToList();
                    if (parts.Count == 0) continue;

                    // A. 处理逻辑行起始行 (s == 0, 包含主键和主要数据)
                    if (s == 0)
                    {
                        int expectedCount = headerGroups[0].Count;
                        int headerOffset = 0;

                        // 状态机路由：依靠切割后的数组长度与表头预期长度对比，来判断数据缺失情况
                        if (parts.Count >= expectedCount)
                        {
                            // 数据完整，包含 Floor 和 Tower
                            lastFloor = parts[0];
                            lastTower = parts[1];
                            headerOffset = 0;
                        }
                        else if (parts.Count == expectedCount - 1)
                        {
                            // 长度差1，说明同楼层存在多个塔，只缺失了 Floor 列
                            lastTower = parts[0];
                            headerOffset = 1; // 数据映射需向右偏移1位，跳过字典中的 Floor 位
                        }
                        else
                        {
                            // 容错兜底：如果是其他未知长度（如底部统计信息被误读），尽可能右对齐映射
                            headerOffset = Math.Max(0, expectedCount - parts.Count);
                        }

                        // 强制填充主键，保证数据血缘完整
                        logicalRow["Floor"] = lastFloor;
                        logicalRow["Tower"] = lastTower;

                        MapPartsToHeaders(parts, headerOffset, headerGroups[s], logicalRow, allColumns);
                    }
                    else
                    {
                        // B. 处理折叠续行 (s > 0)
                        // 续行的数据切割后，其长度天然与 headerGroups[s] 一致，不需要设置偏移量 (offset = 0)
                        MapPartsToHeaders(parts, 0, headerGroups[s], logicalRow, allColumns);
                    }
                }

                // 防止加入空的逻辑行（比如扫到了文件底部的无用统计区域）
                if (logicalRow.Count > 2)
                {
                    rows.Add(logicalRow);
                }
            }

            return new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = block.BlockName,
                Rows = rows,
                Category = DefaultCategory,
                Columns = allColumns.Select(c => new ColumnDefinition(c, c)).ToList()
            };
        }

        /// <summary>
        /// 将多行表头解析为物理行对应的映射组
        /// </summary>
        /// <param name="headerLines">原始表头文本列表</param>
        /// <returns>二维列表，表示每行对应的列名数组</returns>
        private List<List<string>> BuildHeaderGroups(List<string> headerLines)
        {
            var groups = new List<List<string>>();
            foreach (var line in headerLines)
            {
                var parts = Regex.Split(line.Trim(), @"\s+").Where(p => !string.IsNullOrEmpty(p));
                groups.Add(parts.Select(p => MapColumnName(p)).ToList());
            }
            return groups;
        }

        /// <summary>
        /// 执行点对点的数据绑定与特殊转换
        /// </summary>
        /// <param name="parts">物理行拆分后的数值数组</param>
        /// <param name="headerStartIdx">该物理行对应的表头起始索引（用于处理主键缺失带来的左移现象）</param>
        /// <param name="currentHeaders">当前物理行的列名定义集合</param>
        /// <param name="row">目标逻辑行字典</param>
        private void MapPartsToHeaders(List<string> parts, int headerStartIdx, List<string> currentHeaders, Dictionary<string, string> row, HashSet<string> allColumns)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                int logicalIdx = i + headerStartIdx;

                // 防御性判断，防止数组越界
                if (logicalIdx < currentHeaders.Count)
                {
                    string colName = currentHeaders[logicalIdx];
                    string val = parts[i];
                    row[colName] = val;

                    // 触发位移角转换逻辑 (例如 "1/9999." -> "0.000100")
                    if ((colName.Contains("位移角") || colName.Contains("/h") ) && val.Contains('/'))
                    {
                        var driftParts = val.Split('/');
                        // 去除可能存在的小数点，比如 "1/9999."，以防转换失败
                        string denominatorStr = driftParts[1].TrimEnd('.');

                        if (driftParts.Length == 2 && double.TryParse(denominatorStr, out double d) && d != 0)
                        {
                            // 定义新的动态列名
                            string numericColName = colName + "_数值";

                            // 写入数据
                            row[numericColName] = (1.0 / d).ToString("F6");

                            // 【关键修复】同步注册到全局列集合中 (HashSet 自动去重，不用担心重复添加)
                            allColumns.Add(numericColName);
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
            if (raw.Contains("Ratio-(X)")) return "位移比(X)";
            if (raw.Contains("Ratio-(Y)")) return "位移比(Y)";
            if (raw.Contains("Ratio-Dx")) return "层间位移比(X)";
            if (raw.Contains("Ratio-Dy")) return "层间位移比(Y)";
            return raw;
        }
    }
}