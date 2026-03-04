using FileProcessor.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 工程报表构建引擎：提供等宽字体、星号标题、无竖线表格的经典计算书排版
    /// </summary>
    public class EngineeringReportBuilder
    {
        private readonly string _reportTitle;
        private readonly string _versionId;
        private readonly string _note;
        private readonly List<Action<IContainer>> _sections = new();

        public EngineeringReportBuilder(string reportTitle, string versionId, string note)
        {
            _reportTitle = reportTitle;
            _versionId = versionId;
            _note = note;
        }

        /// <summary>
        /// 积木一：添加星号大标题 (例如: ****** SUMMARY OF RESULTS ******)
        /// </summary>
        public void AddSectionTitle(string title)
        {
            _sections.Add(container =>
            {
                container.PaddingTop(20).PaddingBottom(10)
                         .AlignCenter()
                         .Text($"****** {title.ToUpper()} ******")
                         .FontFamily("Courier New")
                         .FontSize(11)
                         .Bold();
            });
        }

        /// <summary>
        /// 积木二：添加硬核数据表格 (防折叠、无竖线)
        /// </summary>
        public void AddEngineeringTable(ProcessedResult result)
        {
            _sections.Add(container =>
            {
                // 动态字号防御：列数越多，字号越小
                float fontSize = result.Columns.Count > 8 ? 8f : 10f;

                container.PaddingVertical(5).Table(table =>
                {
                    // 定义列宽为等比缩放
                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < result.Columns.Count; i++)
                            columns.RelativeColumn();
                    });

                    // 绘制表头 (仅上下有横线)
                    table.Header(header =>
                    {
                        foreach (var col in result.Columns)
                        {
                            header.Cell()
                                  .BorderTop(1).BorderBottom(1).BorderColor(Colors.Black)
                                  .PaddingVertical(3)
                                  .Text(col.Header)
                                  .FontFamily("Courier New").FontSize(fontSize).Bold();
                        }
                    });

                    // 绘制数据行
                    foreach (var row in result.Rows)
                    {
                        for (int c = 0; c < result.Columns.Count; c++)
                        {
                            row.TryGetValue(result.Columns[c].Key, out string? val);

                            var cell = table.Cell().PaddingVertical(2);

                            // 文本靠左，数字靠右，符合工程习惯
                            if (double.TryParse(val, out _))
                                cell.AlignRight().Text(val).FontFamily("Courier New").FontSize(fontSize);
                            else
                                cell.AlignLeft().Text(val ?? "").FontFamily("Courier New").FontSize(fontSize);
                        }
                    }
                });
            });
        }

        /// <summary>
        /// 积木三：通用 DataTable 表格渲染 (用于横向对比表)
        /// </summary>
        public void AddDataTable(System.Data.DataTable dataTable)
        {
            _sections.Add(container =>
            {
                float fontSize = dataTable.Columns.Count > 8 ? 8f : 10f;
                container.PaddingVertical(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < dataTable.Columns.Count; i++) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (System.Data.DataColumn col in dataTable.Columns)
                        {
                            header.Cell().BorderTop(1).BorderBottom(1).BorderColor(Colors.Black)
                                  .PaddingVertical(3).Text(col.ColumnName)
                                  .FontFamily("Courier New").FontSize(fontSize).Bold();
                        }
                    });

                    foreach (System.Data.DataRow row in dataTable.Rows)
                    {
                        for (int c = 0; c < dataTable.Columns.Count; c++)
                        {
                            string val = row[c]?.ToString() ?? "";
                            var cell = table.Cell().PaddingVertical(2);
                            if (double.TryParse(val, out _))
                                cell.AlignRight().Text(val).FontFamily("Courier New").FontSize(fontSize);
                            else
                                cell.AlignLeft().Text(val).FontFamily("Courier New").FontSize(fontSize);
                        }
                    }
                });
            });
        }

        /// <summary>
        /// 复合积木 A：带标题的原始工程数据块 (由通用渲染引擎驱动)
        /// </summary>
        /// <param name="title">数据块标题</param>
        /// <param name="result">包含列定义和字典行数据的解析结果集</param>
        public void AddEngineeringDataBlock(string title, ProcessedResult result)
        {
            // 1. 提取并适配表头
            string[] headers = new string[result.Columns.Count];
            for (int i = 0; i < result.Columns.Count; i++)
            {
                headers[i] = result.Columns[i].Header;
            }

            // 2. 提取并适配数据行
            var rows = new List<string[]>();
            foreach (var r in result.Rows)
            {
                string[] rowValues = new string[result.Columns.Count];
                for (int i = 0; i < result.Columns.Count; i++)
                {
                    r.TryGetValue(result.Columns[i].Key, out string? val);
                    rowValues[i] = val ?? "";
                }
                rows.Add(rowValues);
            }

            // 3. 移交通用底层渲染
            AddGenericTableBlock(title, headers, rows);
        }

        /// <summary>
        /// 复合积木 B：带标题的横向对比透视表 (由通用渲染引擎驱动)
        /// </summary>
        /// <param name="title">数据块标题</param>
        /// <param name="dataTable">WPF前端传来的原生数据透视表</param>
        public void AddComparisonDataBlock(string title, System.Data.DataTable dataTable)
        {
            // 1. 提取并适配表头
            string[] headers = new string[dataTable.Columns.Count];
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                headers[i] = dataTable.Columns[i].ColumnName;
            }

            // 2. 提取并适配数据行
            var rows = new List<string[]>();
            foreach (System.Data.DataRow r in dataTable.Rows)
            {
                string[] rowValues = new string[dataTable.Columns.Count];
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    rowValues[i] = r[i]?.ToString() ?? "";
                }
                rows.Add(rowValues);
            }

            // 3. 移交通用底层渲染
            AddGenericTableBlock(title, headers, rows);
        }

        /// <summary>
        /// 通用表格渲染核心逻辑：处理空间嗅探、智能列宽、对齐与样式绘制
        /// </summary>
        /// <param name="title">表格大标题 (将被转换为大写并添加星号装饰)</param>
        /// <param name="headers">标准化后的表头数组</param>
        /// <param name="rows">标准化后的二维数据行集合</param>
        private void AddGenericTableBlock(string title, string[] headers, List<string[]> rows)
        {
            _sections.Add(container =>
            {
                int columnCount = headers.Length;

                // 🚀 1. 极速嗅探全量数据，计算每一列的最大视觉宽度
                var columnWeights = new int[columnCount];

                // 先用表头宽度垫底
                for (int i = 0; i < columnCount; i++)
                    columnWeights[i] = CalculateStringDisplayWidth(headers[i]);

                // 遍历数据行，寻找最宽的字符串
                foreach (var row in rows)
                {
                    for (int i = 0; i < columnCount; i++)
                    {
                        int w = CalculateStringDisplayWidth(row[i]);
                        if (w > columnWeights[i]) columnWeights[i] = w;
                    }
                }

                // 空间嗅探：预留 120 单位防止标题与表格脱节
                container.EnsureSpace(120).Column(col =>
                {
                    // 绘制硬核工程大标题
                    col.Item().PaddingTop(20).PaddingBottom(10).AlignCenter()
                       .Text($"****** {title.ToUpper()} ******")
                       .FontFamily("Courier New").FontSize(11).Bold();

                    // 动态字号与字体策略
                    float fontSize = columnCount > 8 ? 8f : 10f;
                    string fontName = columnCount > 8 ? "Consolas" : "Courier New";

                    col.Item().Table(table =>
                    {
                        // 🚀 2. 按照嗅探出的最大宽度，按比例智能分配列宽（+2 留出呼吸边缘）
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < columnCount; i++)
                                columns.RelativeColumn(columnWeights[i] + 2);
                        });

                        // 绘制表头 (上下边框、居中、加粗)
                        table.Header(header =>
                        {
                            for (int i = 0; i < columnCount; i++)
                            {
                                header.Cell().BorderTop(1).BorderBottom(1).BorderColor(Colors.Black)
                                      .PaddingVertical(3)
                                      .AlignCenter()
                                      .Text(headers[i])
                                      .FontFamily("Courier New").FontSize(fontSize).Bold();
                            }
                        });

                        // 绘制数据行
                        foreach (var row in rows)
                        {
                            for (int c = 0; c < columnCount; c++)
                            {
                                string val = row[c];
                                var cell = table.Cell().PaddingVertical(2);

                                // 智能对齐：数字靠右，纯文本居中
                                if (double.TryParse(val, out _))
                                {
                                    cell.AlignRight().Text(val)
                                        .FontFamily(fontName).FontSize(fontSize);
                                        //.FontColor(Colors.Grey.Darken3);
                                }
                                else
                                {
                                    cell.AlignCenter().Text(val)
                                        .FontFamily(fontName).FontSize(fontSize);
                                        //.FontColor(Colors.Grey.Darken3);
                                }
                            }
                        }
                    });
                });
            });
        }

        /// <summary>
        /// 拼装并生成最终的 PDF 文件
        /// </summary>
        public void BuildAndSave(string filePath, bool useLandscape = false)
        {
            // QuestPDF 强制要求声明社区版使用许可
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // 页面设置：智能决定横向还是纵向，标准 A4
                    page.Size(useLandscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                    //page.Margin(40);

                    // 【修改点：精细化控制页边距】
                    // QuestPDF 的默认单位是 Point (1 cm ≈ 28.35 Points)。原来是全局 Margin(40)。
                    page.MarginLeft(5);   // 缩小左边距（从 40 减半到 20）
                    page.MarginRight(5);  // 缩小右边距（从 40 减半到 20）
                    page.MarginTop(68);    // 增加顶边距（原 40 + 28.35 ≈ 68，即增加了约 1cm）
                    page.MarginBottom(40); // 底边距保持原有比例不变

                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(10));

                    // 装载页眉
                    page.Header().Element(ComposeHeader);

                    // 装载所有积木内容
                    page.Content().Element(ComposeContent);

                    // 装载页脚
                    page.Footer().Element(ComposeFooter);
                });
            });

            document.GeneratePdf(filePath);
        }

        private void ComposeHeader(IContainer container)
        {
            container.BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Job ID      : {_versionId}").FontFamily("Courier New").Bold();
                    col.Item().Text($"Description : {_reportTitle}").FontFamily("Courier New");
                });

                row.ConstantItem(250).AlignRight().Column(col =>
                {
                    col.Item().Text("YJK Data Monitoring Center").FontFamily("Courier New").Bold();
                    col.Item().Text($"Note : {_note}").FontFamily("Courier New");
                    col.Item().Text($"Date : {DateTime.Now:MM/dd/yyyy h:mm tt}").FontFamily("Courier New");
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(col =>
            {
                foreach (var section in _sections)
                {
                    col.Item().Element(section);
                }
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Page ").FontFamily("Courier New").FontSize(9);
                x.CurrentPageNumber().FontFamily("Courier New").FontSize(9);
                x.Span(" of ").FontFamily("Courier New").FontSize(9);
                x.TotalPages().FontFamily("Courier New").FontSize(9);
            });
        }

        /// <summary>
        /// 计算字符串在等宽字体下的视觉物理宽度（汉字算2，英文字母/数字算1）
        /// </summary>
        private int CalculateStringDisplayWidth(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 1; // 兜底最小宽度
            int width = 0;
            foreach (char c in text)
            {
                // ASCII 字符算 1 个宽度，其他（如中文字符）算 2 个宽度
                width += c > 127 ? 2 : 1;
            }
            return width;
        }
    }
}