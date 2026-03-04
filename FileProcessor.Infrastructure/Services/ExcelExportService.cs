using ClosedXML.Excel;
using FileProcessor.Core.Helpers;
using FileProcessor.Core.Models;
using FileProcessor.DebugHelpers;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// Excel 导出服务：提供高度解耦的表格生成、数据映射与归档功能
    /// </summary>
    public class ExcelExportService
    {
        /// <summary>
        /// 核心需求：按文件分组导出整个版本的数据，生成带超链接目录的 Excel 文件集
        /// </summary>
        /// <param name="targetFolder">用户选择的导出目标文件夹</param>
        /// <param name="versionId">当前版本号</param>
        /// <param name="note">用户填写的备注</param>
        /// <param name="versionData">该版本下的所有解析数据</param>
        public async Task ExportVersionDataAsync(string targetFolder, string versionId, string note, IEnumerable<ProcessedResult> versionData)
        {
            if (string.IsNullOrEmpty(targetFolder) || !versionData.Any()) return;

            string safeNote = SanitizeForFileName(note);
            string suffix = string.IsNullOrWhiteSpace(safeNote) ? versionId : $"{versionId}_{safeNote}";

            // 按所属源文件 (SourceFileName) 分组，每个源文件生成一个 Excel
            var fileGroups = versionData.GroupBy(r => r.SourceFileName).ToList();

            // 放到后台线程池执行，防止卡死 UI
            await Task.Run(() =>
            {
                foreach (var group in fileGroups)
                {
                    string safeSourceName = SanitizeForFileName(Path.GetFileNameWithoutExtension(group.Key));
                    string excelFileName = $"{safeSourceName}_{suffix}.xlsx";
                    string excelFilePath = Path.Combine(targetFolder, excelFileName);

                    using var workbook = new XLWorkbook();

                    // 1. 创建神来之笔：总目录 Sheet
                    var indexSheet = workbook.Worksheets.Add("目录");
                    SetupIndexSheetHeader(indexSheet);

                    int sheetCounter = 1;
                    int indexRow = 2; // 目录数据从第 2 行开始

                    // 2. 遍历该文件下的所有数据块，生成独立 Sheet
                    foreach (var result in group)
                    {
                        // 生成安全的、不超过 31 字符的短名称 (如: 工况9_X方向..._01)
                        string safeSheetName = GenerateSafeSheetName(result.StandardBlockName, sheetCounter);

                        var dataSheet = workbook.Worksheets.Add(safeSheetName);

                        // 将业务数据填充到该 Sheet
                        FillDataSheet(dataSheet, result);

                        // 3. 在目录页添加记录与超链接
                        indexSheet.Cell(indexRow, 1).Value = sheetCounter;
                        indexSheet.Cell(indexRow, 2).Value = result.StandardBlockName; // 完整名字
                        indexSheet.Cell(indexRow, 3).Value = "点击跳转 ->";

                        // 建立超链接，指向对应工作表的 A1 单元格
                        indexSheet.Cell(indexRow, 3).SetHyperlink(new XLHyperlink($"'{safeSheetName}'!A1"));
                        indexSheet.Cell(indexRow, 3).Style.Font.FontColor = XLColor.Blue;
                        indexSheet.Cell(indexRow, 3).Style.Font.Underline = XLFontUnderlineValues.Single;
                        indexSheet.Cell(indexRow, 3).Style.Font.FontName = "Courier New";

                        sheetCounter++;
                        indexRow++;
                    }

                    // 自动调整目录页的列宽以适应超长文本
                    indexSheet.Columns().AdjustToContents();

                    // 保存此文件的 Excel
                    workbook.SaveAs(excelFilePath);
                    Log.Debug($"[ExcelExport] 成功导出文件: {excelFileName}");
                }
            });
        }

        /// <summary>
        /// 【预留能力】：通用 DataTable 导出接口（用于后续对比表和独立 Slot 的右键导出）
        /// </summary>
        public void ExportDataTableToExcel(DataTable dataTable, string filePath, string sheetName = "Data")
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(SanitizeForSheetName(sheetName, 31));
            ws.Cell(1, 1).InsertTable(dataTable);
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// 使用 EPPlus 导出插槽数据 (支持无损追加与原生图表生成)
        /// </summary>
        public void ExportSlotToExcel(System.Data.DataTable dataTable, string filePath, string slotTitle, bool isChart)
        {
            if (dataTable == null || dataTable.Rows.Count == 0) return;

            // 清洗 Sheet 名称（去除不可用字符，限制长度 < 31）
            string safeSheetName = string.IsNullOrWhiteSpace(slotTitle) ? "SlotData" : string.Join("_", slotTitle.Split(Path.GetInvalidFileNameChars())).Replace(":", "").Replace("?", "").Replace("*", "").Replace("/", "");
            if (safeSheetName.Length > 28) safeSheetName = safeSheetName.Substring(0, 28);

            FileInfo fileInfo = new FileInfo(filePath);

            // 核心特性：文件存在则加载追加，不存在则自动新建
            using (var package = new ExcelPackage(fileInfo))
            {
                // 防止追加时 Sheet 同名冲突
                string finalSheetName = safeSheetName;
                int counter = 1;
                while (package.Workbook.Worksheets.Any(ws => ws.Name == finalSheetName))
                {
                    finalSheetName = $"{safeSheetName}_{counter}";
                    counter++;
                }

                var sheet = package.Workbook.Worksheets.Add(finalSheetName);

                // 1. 无论图表还是表格，先把纯净数据静默写入 A1 单元格开始的位置
                sheet.Cells["A1"].LoadFromDataTable(dataTable, true);

                int rowCount = dataTable.Rows.Count;
                int colCount = dataTable.Columns.Count;

                if (!isChart)
                {
                    // 🌈 纯表格模式：美化表头并冻结首行
                    var headerRange = sheet.Cells[1, 1, 1, colCount];
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                    sheet.View.FreezePanes(2, 1);
                }
                else
                {
                    // 📈 图表模式：基于刚才写入的数据，生成真正的 Excel 折线图
                    var chart = sheet.Drawings.AddChart($"{finalSheetName}_Chart", eChartType.XYScatterLines);
                    chart.Title.Text = slotTitle;

                    // 将图表放置在数据的右侧 (从第3列开始)
                    chart.SetPosition(1, 0, colCount + 1, 0);
                    chart.SetSize(600, 400);

                    // 绑定数据系列 (第1列是 X 轴，第2列是 Y 轴)
                    var xRange = sheet.Cells[2, 1, rowCount + 1, 1];
                    var yRange = sheet.Cells[2, 2, rowCount + 1, 2];

                    var series = chart.Series.Add(yRange, xRange);
                    series.Header = dataTable.Columns[0].ColumnName; // 设置图例名称

                    chart.Style = eChartStyle.Style2;
                    foreach (ExcelChartAxis axis in chart.Axis)
                        axis.MinorTickMark = eAxisTickMark.None;

                    // 【可选美化】：隐藏基础数据列，让 Excel 里只显示一个干净的图表
                    // sheet.Column(1).Hidden = true;
                    // sheet.Column(2).Hidden = true;
                }

                package.Save();
            }
        }

        /// <summary>
        /// 导出对比表：支持多系列散点图 (XY Scatter) 与强制覆盖
        /// </summary>
        public void ExportComparisonToExcel(System.Data.DataTable dataTable, string filePath, string sheetTitle, bool overWrite)
        {
            if (dataTable == null || dataTable.Rows.Count == 0) return;

            // 1. 【覆盖策略】：杜绝新建变追加
            if (overWrite && File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            FileInfo fileInfo = new FileInfo(filePath);
            using (var package = new ExcelPackage(fileInfo))
            {

                // 🚀 调用统一工具，1行代码搞定原先5行干的事
                string safeName = FileHelper.GetSafeSheetName(sheetTitle, 26);

                string finalSheetName = safeName;
                int counter = 1;
                while (package.Workbook.Worksheets.Any(ws => ws.Name == finalSheetName))
                {
                    finalSheetName = $"{safeName}_{counter}";
                    counter++;
                }

                var sheet = package.Workbook.Worksheets.Add(finalSheetName);

                // 2. 写入“宽表”数据
                sheet.Cells["A1"].LoadFromDataTable(dataTable, true);
                int rowCount = dataTable.Rows.Count;
                int colCount = dataTable.Columns.Count;

                // 3. 【散点连线图】引擎介入
                var chart = sheet.Drawings.AddChart($"{finalSheetName}_Chart", eChartType.XYScatterLines);
                chart.Title.Text = sheetTitle;
                chart.SetPosition(1, 0, colCount + 1, 0);
                chart.SetSize(800, 500);

                // 4. 【所见即所得】：完美映射坐标轴
                // 宽表第 1 列 (A列) 存放的是基准轴(如楼层)。
                // 按照界面习惯，我们把它绑定给 Excel 散点图的 YSeries (垂直方向)
                var yRange = sheet.Cells[2, 1, rowCount + 1, 1];

                // 从第 2 列开始，每一列代表一个版本的对比数据
                // 我们把它们绑定给散点图的 XSeries (水平方向)
                for (int i = 1; i < colCount; i++)
                {
                    var xRange = sheet.Cells[2, i + 1, rowCount + 1, i + 1];
                    // EPPlus 绑定语法：Series.Add(Y坐标序列, X坐标序列)
                    var series = chart.Series.Add(yRange, xRange);
                    series.Header = dataTable.Columns[i].ColumnName; // 版本名作为图例
                }

                package.Save();
            }
        }

        #region 私有辅助方法

        private void SetupIndexSheetHeader(IXLWorksheet indexSheet)
        {
            indexSheet.Cell(1, 1).Value = "序号";
            indexSheet.Cell(1, 2).Value = "数据块完整名称 (YJK)";
            indexSheet.Cell(1, 3).Value = "快捷跳转";

            var headerRange = indexSheet.Range("A1:C1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        private void FillDataSheet(IXLWorksheet ws, ProcessedResult result)
        {
            // 写入表头
            for (int i = 0; i < result.Columns.Count; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = result.Columns[i].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightCyan;
            }

            // 写入数据行
            for (int r = 0; r < result.Rows.Count; r++)
            {
                var rowData = result.Rows[r];
                for (int c = 0; c < result.Columns.Count; c++)
                {
                    string colKey = result.Columns[c].Key;
                    if (rowData.TryGetValue(colKey, out string? val))
                    {
                        // 尝试转为数字以利用 Excel 的数值能力，如果失败则按文本存
                        if (double.TryParse(val, out double numVal))
                            ws.Cell(r + 2, c + 1).Value = numVal;
                        else
                            ws.Cell(r + 2, c + 1).Value = val;
                    }
                }
            }
            // 冻结首行，方便向下滚动
            ws.SheetView.FreezeRows(1);
        }

        private string SanitizeForFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            return Regex.Replace(input, string.Format(@"[{0}]", invalidChars), "_");
        }

        private string GenerateSafeSheetName(string originalName, int index)
        {
            string clean = SanitizeForSheetName(originalName, 26); // 预留 5 个字符给后缀
            string suffix = $"_{index:D2}";
            return $"{clean}{suffix}";
        }

        private string SanitizeForSheetName(string input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Sheet";
            // Excel Sheet 名不可包含 : \ / ? * [ ]
            string clean = Regex.Replace(input, @"[:\\/?*\[\]]", "");
            return clean.Length > maxLength ? clean.Substring(0, maxLength) : clean;
        }

        #endregion
    }
}