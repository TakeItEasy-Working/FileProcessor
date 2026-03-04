using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.DebugHelpers;
using FileProcessor.Desktop.Models;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using FileProcessor.Core.Helpers;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;

// 强行指定 ColumnDefinition 的归属
using ColumnDefinition = FileProcessor.Core.Models.ColumnDefinition;
using MessageBox = System.Windows.MessageBox;

namespace FileProcessor.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DataCoordinator _coordinator;
        private readonly ProjectMonitorService _monitor;
        private readonly IVersionCoordinator _versionCoordinator;

        // 【新增】：初始化打包归档服务
        private readonly ArchiveService _archiveService = new();
        private readonly ExcelExportService _excelExportService = new(); // 新增：Excel 导出服务
        private readonly PdfExportService _pdfExportService = new();

        public MainViewModel(DataCoordinator coordinator, ProjectMonitorService monitor, IVersionCoordinator versionCoordinator)
        {
            _coordinator = coordinator;
            _monitor = monitor;
            _versionCoordinator = versionCoordinator;

            _versionCoordinator.VersionCommitted += OnVersionCommitted;

            // 初始化 8 个插槽
            for (int i = 0; i < 8; i++)
                Slots.Add(new SlotViewModel(_coordinator, i));

            Log.Debug("[MainViewModel] 初始化成功...");
            StatusText = "就绪。请选择项目目录...";
            StatusColor = "#9E9E9E";
        }

        public ObservableCollection<SlotViewModel> Slots { get; } = new();

        /// <summary>
        /// 动态可用的塔号列表。系统初始化时永远保留一个全局视角的 "All" 选项。
        /// </summary>
        public ObservableCollection<string> AvailableTowers { get; } = new ObservableCollection<string> { "All" };

        // 升级为对象集合
        public ObservableCollection<VersionRecord> VersionHistory { get; } = new();

        [ObservableProperty] private string? _projectPath;
        [ObservableProperty] private VersionRecord? _selectedVersion; // 升级为对象类型
        [ObservableProperty] private string _currentTower = "All";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _statusColor = "";

        [RelayCommand]
        private void SelectProject()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectPath = dialog.SelectedPath;
                StartProjectMonitoring(dialog.SelectedPath);
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (SelectedVersion == null || string.IsNullOrEmpty(SelectedVersion.VersionId) || SelectedVersion.VersionId == "Live_Standby")
            {
                MessageBox.Show("请先在左侧选择一个有效的版本进行导出！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 1. 让用户选择保存的文件夹
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "请选择导出的 Excel 文件存放目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string targetFolder = dialog.SelectedPath;

            try
            {
                // UI 反馈：开始导出
                StatusText = $"正在导出版本 {SelectedVersion.VersionId} ...";
                StatusColor = "#3498DB"; // 蓝色表示执行中

                // 2. 向底层枢纽索取该版本的所有完整数据
                // (注意：需要在 DataCoordinator 中添加此通道方法)
                var versionData = _coordinator.GetFullVersionData(SelectedVersion.VersionId);

                if (!versionData.Any())
                {
                    MessageBox.Show("该版本下没有可导出的数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusText = "导出取消：无数据";
                    StatusColor = "#E74C3C";
                    return;
                }

                // 3. 呼叫 Excel 导出服务
                await _excelExportService.ExportVersionDataAsync(targetFolder, SelectedVersion.VersionId, SelectedVersion.Note, versionData);

                // UI 反馈：导出成功
                StatusText = $"导出完成！存放在: {Path.GetFileName(targetFolder)}";
                StatusColor = "#2ECC71";
                MessageBox.Show("Excel 数据导出成功！\n请前往所选文件夹查看。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = "导出发生异常";
                StatusColor = "#E74C3C";
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            if (SelectedVersion == null || string.IsNullOrEmpty(SelectedVersion.VersionId) || SelectedVersion.VersionId == "Live_Standby")
            {
                MessageBox.Show("请先在左侧选择一个有效的版本进行导出！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "请选择导出的 PDF 计算书存放目录", ShowNewFolderButton = true };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            try
            {
                StatusText = $"正在生成 PDF 计算书: {SelectedVersion.VersionId} ...";
                StatusColor = "#3498DB";

                var versionData = _coordinator.GetFullVersionData(SelectedVersion.VersionId);
                await _pdfExportService.ExportVersionDataToPdfAsync(dialog.SelectedPath, SelectedVersion.VersionId, SelectedVersion.Note, versionData);

                StatusText = $"PDF 导出完成！";
                StatusColor = "#2ECC71";
                MessageBox.Show("PDF 计算书导出成功！\n请前往所选文件夹查看。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = "导出发生异常"; StatusColor = "#E74C3C";
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出右侧多版本横向对比透视表
        /// 绑定的 Command 为: ExportComparisonCommand
        /// </summary>
        [RelayCommand]
        private async Task ExportComparisonAsync()
        {
            // 1. 数据校验：直接使用 UI 绑定的现成表格，如果没有生成表格，提示用户
            if (CompDisplayTable == null || CompDisplayTable.Rows.Count == 0)
            {
                MessageBox.Show("当前没有可导出的对比数据，请先在上方选择有效的文件和参数。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // =========================================================================
            // 🚀 核心升级：基于现有 UI 表格，克隆一个“强类型 (object)”的干净表格
            // 目的：让 Excel 认出数字，解决“文本格式”带绿三角的问题
            // =========================================================================
            DataTable cleanTable = new DataTable();

            // 完全复刻表头，但强制类型为 object
            foreach (DataColumn col in CompDisplayTable.Columns)
            {
                cleanTable.Columns.Add(col.ColumnName, typeof(object));
            }

            // 复刻每一行数据，并用我们提炼的 DRY 工具清洗数值
            foreach (DataRow row in CompDisplayTable.Rows)
            {
                DataRow newRow = cleanTable.NewRow();
                for (int i = 0; i < CompDisplayTable.Columns.Count; i++)
                {
                    string? rawValue = row[i]?.ToString();
                    newRow[i] = FileProcessor.Core.Helpers.DataFormatHelper.ParseEngineNumber(rawValue);
                }
                cleanTable.Rows.Add(newRow);
            }
            // =========================================================================

            // 2. 利用统一工具清洗文件名
            string safeFile = FileHelper.GetSafeFileName(CompSelectedFile);
            string safeBlock = FileHelper.GetSafeFileName(CompSelectedBlock);
            string shortBlock = safeBlock.Length > 30 ? safeBlock.Substring(0, 30) : safeBlock;
            
            // 3. 呼出统一弹窗
            string? filePath = PromptSaveFileDialog("导出全版本对比", "Excel (*.xlsx)|*.xlsx|PDF (*.pdf)|*.pdf", $"横向对比_{safeFile}_{shortBlock}");
            if (filePath == null) return; // 用户点击了取消

            bool isPdf = filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            // 4. 调用统一包装器执行底层任务
            await ExecuteExportWrapperAsync("正在导出对比数据...", "横向对比数据已成功导出！", async () =>
            {
                if (isPdf)
                {
                    _pdfExportService.ExportDataTableToPdf(cleanTable, filePath, safeFile, $"参数: {shortBlock}");
                }
                else
                {
                    _excelExportService.ExportComparisonToExcel(cleanTable, filePath, shortBlock, true);
                }
            });
        }

        /// <summary>
        /// 右键菜单：将插槽数据导出为新的 Excel 文件
        /// </summary>
        [RelayCommand]
        private async Task ExportSlotToNewExcelAsync(SlotViewModel slot)
        {
            if (slot == null || slot.Status != FileProcessor.Mediator.Models.SlotStatus.Ready) return;

            var dt = slot.GetExportDataTable();
            if (dt == null) return;

            string safeTitle = string.IsNullOrWhiteSpace(slot.SelectedBlock) ? "Data" : slot.SelectedBlock;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出插槽数据",
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = $"插槽数据_{safeTitle}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                await ExecuteSlotExportAsync(dt, dialog.FileName, safeTitle, slot.IsChartMode);
            }
        }

        /// <summary>
        /// 右键菜单：将插槽数据追加到现有的 Excel 文件中
        /// </summary>
        [RelayCommand]
        private async Task AppendSlotToExcelAsync(SlotViewModel slot)
        {
            if (slot == null || slot.Status != FileProcessor.Mediator.Models.SlotStatus.Ready) return;

            var dt = slot.GetExportDataTable();
            if (dt == null) return;

            string safeTitle = string.IsNullOrWhiteSpace(slot.SelectedBlock) ? "Data" : slot.SelectedBlock;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要追加的 Excel 文件",
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                await ExecuteSlotExportAsync(dt, dialog.FileName, safeTitle, slot.IsChartMode);
            }
        }

        private async Task ExecuteSlotExportAsync(DataTable dt, string filePath, string title, bool isChart)
        {
            try
            {
                StatusText = "正在导出插槽数据..."; StatusColor = "#3498DB";

                await Task.Run(() =>
                {
                    // 调用刚才写的 EPPlus 服务
                    _excelExportService.ExportSlotToExcel(dt, filePath, title, isChart);
                });

                StatusText = "插槽导出完成！"; StatusColor = "#2ECC71";
                // MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = "导出失败"; StatusColor = "#E74C3C";
                MessageBox.Show($"导出失败：文件可能被占用。\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartProjectMonitoring(string path)
        {
            try
            {
                StatusText = "正在清理历史数据并初始化引擎...";
                StatusColor = "#F39C12";

                // 1. 命令底层彻底清空旧项目结果
                _coordinator.ClearAllData();

                // 2. 清理 UI 层的版本历史
                VersionHistory.Clear();
                SelectedVersion = null;

                // 【新增】：重置塔号列表，归位到默认状态
                AvailableTowers.Clear();
                AvailableTowers.Add("All");
                CurrentTower = "All";

                // 【修复】：用初始对象代替原来的 "LIVE 实时状态" 字符串
                var standbyRecord = new VersionRecord
                {
                    VersionId = "Live_Standby",
                    Note = "等待实时数据...",
                    FileCount = 0
                };
                VersionHistory.Add(standbyRecord);

                CompAvailableFiles.Clear();
                CompAvailableBlocks.Clear();
                CompDisplayTable = null;
                CompChartSeries = null;

                // 3. 强行重置所有插槽
                foreach (var slot in Slots)
                {
                    slot.SelectedFile = null;
                    slot.SelectedBlock = null;
                    slot.AvailableFiles.Clear();
                    slot.AvailableBlocks.Clear();
                    slot.DisplayTable = null;
                    slot.ChartSeries = null;
                    slot.Status = FileProcessor.Mediator.Models.SlotStatus.Empty;
                }

                // 4. 启动监控
                await Task.Run(() => _monitor.StartScanning(path));

                StatusText = $"监控运行中: {System.IO.Path.GetFileName(path)}";
                StatusColor = "#2ECC71";
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动监控失败: " + ex.Message);
                StatusText = "初始化失败";
                StatusColor = "#E74C3C";
            }
        }

        /// <summary>
        /// 当引擎提交新版本（如 INITIAL_SCAN 或 Batch_xxx）时触发
        /// </summary>
        private async void OnVersionCommitted(string versionId)
        {
            await Task.Delay(1500);

            // 切换到 UI 线程更新集合
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 【修复】：查询集合中是否已存在该对象
                var existing = VersionHistory.FirstOrDefault(v => v.VersionId == versionId);

                if (existing == null)
                {
                    // 【新增】：嗅探新版本中的所有塔号，并动态追加到下拉列表中
                    var detectedTowers = _coordinator.GetAvailableTowers(versionId);
                    foreach (var tower in detectedTowers)
                    {
                        if (!AvailableTowers.Contains(tower))
                        {
                            AvailableTowers.Add(tower);
                        }
                    }

                    // 向底层查询此版本解析的真实文件数
                    int parsedCount = _coordinator.GetParsedFileCount(versionId);

                    // 【修改】：使用 StartsWith 来识别初始版本
                    string defaultNote = versionId.StartsWith("Init_") ? "初始模型状态" : "";

                    var newRecord = new VersionRecord
                    {
                        VersionId = versionId,
                        FileCount = parsedCount,
                        Note = defaultNote
                    };

                    VersionHistory.Add(newRecord);

                    // 【新增】：核心联动 —— 自动打包与 Note 监听机制
                    // 仅对 Batch 批次计算 或 初始扫描 进行打包（过滤掉 Live 散件）
                    if (versionId.StartsWith("Batch_") || versionId.StartsWith("Init_"))
                    {
                        // 1. 扔到后台线程静默打包，绝不卡顿 UI
                        Task.Run(() => _archiveService.CreateArchiveAsync(ProjectPath!, versionId, newRecord.Note));

                        // 2. 挂载监听器：一旦用户在 UI 修改了 Note，立刻指挥后台重命名压缩包
                        newRecord.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(VersionRecord.Note))
                            {
                                _archiveService.UpdateArchiveNote(ProjectPath!, newRecord.VersionId, newRecord.Note);
                            }
                        };
                    }

                    // 如果是初始扫描，或者当前没选中任何版本，自动选中它
                    if (versionId.StartsWith("Init_") || SelectedVersion == null || SelectedVersion.VersionId == "Live_Standby")
                    {
                        SelectedVersion = newRecord;
                    }
                }

                // 刷新所有插槽
                foreach (var slot in Slots)
                {
                    slot.RefreshFileList();
                }

                RefreshCompFileList();
            });
        }

        partial void OnSelectedVersionChanged(VersionRecord? value)
        {
            if (value == null || string.IsNullOrEmpty(value.VersionId)) return;

            // 当选择待机占位符时，不做处理
            if (value.VersionId == "Live_Standby") return;

            _coordinator.SwitchViewVersion(value.VersionId);

            foreach (var slot in Slots) slot.RefreshFileList();
        }

        partial void OnCurrentTowerChanged(string value)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                _coordinator.SetTowerFilter(i, value);
            }
            BuildComparisonData();
        }

        #region 趋势与对比

        public ObservableCollection<string> CompAvailableFiles { get; } = new();
        public ObservableCollection<string> CompAvailableBlocks { get; } = new();
        public ObservableCollection<ColumnDefinition> CompAvailableColumns { get; } = new();

        [ObservableProperty] private string? _compSelectedFile;
        [ObservableProperty] private string? _compSelectedBlock;
        [ObservableProperty] private ColumnDefinition? _compSelectedYAxis;
        [ObservableProperty] private ColumnDefinition? _compSelectedXAxis;

        [ObservableProperty] private DataTable? _compDisplayTable;
        [ObservableProperty] private SeriesCollection? _compChartSeries;
        [ObservableProperty] private string _compXAxisTitle = "";
        [ObservableProperty] private string _compYAxisTitle = "";

        private void RefreshCompFileList()
        {
            var cached = CompSelectedFile;
            CompAvailableFiles.Clear();
            foreach (var f in _coordinator.GetAvailableFiles())
            {
                CompAvailableFiles.Add(f);
            }
            if (!string.IsNullOrEmpty(cached) && CompAvailableFiles.Contains(cached))
            {
                CompSelectedFile = cached;
            }
        }

        partial void OnCompSelectedFileChanged(string? value)
        {
            CompAvailableBlocks.Clear();
            CompSelectedBlock = null;
            if (string.IsNullOrEmpty(value)) return;
            foreach (var b in _coordinator.GetBlocksForFile(value))
            {
                CompAvailableBlocks.Add(b);
            }
        }

        partial void OnCompSelectedBlockChanged(string? value)
        {
            CompAvailableColumns.Clear();
            CompSelectedYAxis = null;
            CompSelectedXAxis = null;
            if (string.IsNullOrEmpty(CompSelectedFile) || string.IsNullOrEmpty(value)) return;

            var history = _coordinator.GetCrossVersionHistory(CompSelectedFile, value).ToList();
            if (!history.Any()) return;

            var referenceVersion = history.Last();
            foreach (var col in referenceVersion.Columns)
            {
                CompAvailableColumns.Add(col);
            }

            var yCol = CompAvailableColumns.FirstOrDefault(c =>
                c.Key.Contains("Floor") || c.Header.Contains("层") || c.Header.Contains("振型"));

            var xCol = CompAvailableColumns.FirstOrDefault(c =>
                c != yCol && !c.Key.Contains("Tower") &&
                (c.Key.Contains("位移") || c.Key.Contains("数值") || c.Key.Contains("Max") || c.Key.Contains("周期")));

            CompSelectedYAxis = yCol ?? CompAvailableColumns.FirstOrDefault();
            CompSelectedXAxis = xCol ?? CompAvailableColumns.Skip(1).FirstOrDefault();
        }

        partial void OnCompSelectedYAxisChanged(ColumnDefinition? value) => BuildComparisonData();
        partial void OnCompSelectedXAxisChanged(ColumnDefinition? value) => BuildComparisonData();

        private void BuildComparisonData()
        {
            if (string.IsNullOrEmpty(CompSelectedFile) || string.IsNullOrEmpty(CompSelectedBlock) ||
                CompSelectedYAxis == null || CompSelectedXAxis == null)
            {
                CompDisplayTable = null;
                CompChartSeries = null;
                return;
            }

            var rawHistory = _coordinator.GetCrossVersionHistory(CompSelectedFile, CompSelectedBlock).ToList();
            if (!rawHistory.Any()) return;

            // ==========================================
            // 【新增排序逻辑】：解决版本乱序问题
            // 根据左侧 VersionHistory (真实加入时间序) 对提取到的历史数据进行重排
            // ==========================================
            var orderedVersionIds = VersionHistory.Select(v => v.VersionId).ToList();
            var history = rawHistory.OrderBy(h =>
                {
                    int index = orderedVersionIds.IndexOf(h.VersionId);
                    return index == -1 ? int.MaxValue : index; // 找不到的排在最后
                })
                .ToList();

            var yValuesSet = new HashSet<string>();
            foreach (var res in history)
            {
                foreach (var row in res.Rows)
                {
                    if (IsRowMatchCurrentTower(row) && row.TryGetValue(CompSelectedYAxis.Key, out var yVal))
                    {
                        yValuesSet.Add(yVal);
                    }
                }
            }

            var sortedYValues = yValuesSet.Select(y =>
            {
                bool isNum = double.TryParse(y, out double d);
                return new { Original = y, IsNum = isNum, NumVal = d };
            })
            .OrderBy(y => y.IsNum ? 0 : 1)
            .ThenBy(y => y.NumVal)
            .ThenBy(y => y.Original)
            .Select(y => y.Original)
            .ToList();

            var dt = new DataTable();
            dt.Columns.Add(new DataColumn(CompSelectedYAxis.Header));
            // 【升级】：动态查找并拼接用户注释作为列名
            foreach (var res in history)
            {
                var record = VersionHistory.FirstOrDefault(v => v.VersionId == res.VersionId);
                string colName = res.VersionId;

                // 如果该版本有用户填写的有效备注，则拼接到列名中
                if (record != null && !string.IsNullOrWhiteSpace(record.Note))
                {
                    colName = $"{res.VersionId} ({record.Note})";
                }

                dt.Columns.Add(new DataColumn(colName));
            }

            foreach (var y in sortedYValues)
            {
                var row = dt.NewRow();
                row[0] = y;
                for (int i = 0; i < history.Count; i++)
                {
                    var dataRow = history[i].Rows.FirstOrDefault(r =>
                        IsRowMatchCurrentTower(r) &&
                        r.TryGetValue(CompSelectedYAxis.Key, out var val) && val == y);

                    if (dataRow != null && dataRow.TryGetValue(CompSelectedXAxis.Key, out var xVal))
                    {
                        row[i + 1] = xVal;
                    }
                    else
                    {
                        row[i + 1] = string.Empty;
                    }
                }
                dt.Rows.Add(row);
            }
            CompDisplayTable = dt;

            var seriesCollection = new SeriesCollection();
            foreach (var res in history)
            {
                var values = new ChartValues<ObservablePoint>();
                foreach (var y in sortedYValues)
                {
                    var dataRow = res.Rows.FirstOrDefault(r =>
                        IsRowMatchCurrentTower(r) &&
                        r.TryGetValue(CompSelectedYAxis.Key, out var val) && val == y);

                    if (dataRow != null &&
                        dataRow.TryGetValue(CompSelectedXAxis.Key, out var xStr) &&
                        double.TryParse(xStr, out double xNum) &&
                        double.TryParse(y, out double yNum))
                    {
                        values.Add(new ObservablePoint(xNum, yNum));
                    }
                }

                if (values.Any())
                {
                    // 【升级】：图表图例也同步带上用户注释
                    var record = VersionHistory.FirstOrDefault(v => v.VersionId == res.VersionId);
                    string legendTitle = res.VersionId;
                    if (record != null && !string.IsNullOrWhiteSpace(record.Note))
                    {
                        legendTitle = $"{res.VersionId} ({record.Note})";
                    }

                    seriesCollection.Add(new LineSeries
                    {
                        Title = legendTitle, // 图例显示版本号 + 注释
                        Values = values,
                        PointGeometrySize = 8,
                        LineSmoothness = 0
                    });
                }
            }

            CompChartSeries = seriesCollection;
            CompXAxisTitle = CompSelectedXAxis.Header;
            CompYAxisTitle = CompSelectedYAxis.Header;
        }

        private bool IsRowMatchCurrentTower(Dictionary<string, string> row)
        {
            if (string.Equals(CurrentTower, "All", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!row.TryGetValue("Tower", out var rowTower))
                return true;

            return string.Equals(rowTower, CurrentTower, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 🚀 核心基础设施包装器 (DRY)

        /// <summary>
        /// 统一的保存弹窗调度器
        /// </summary>
        private string? PromptSaveFileDialog(string title, string filter, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// 统一的异步导出状态包装器（干掉所有重复的 Try-Catch 和 Status 更新）
        /// </summary>
        private async Task ExecuteExportWrapperAsync(string startMsg, string successMsg, Func<Task> exportAction)
        {
            try
            {
                StatusText = startMsg; StatusColor = "#3498DB";

                await Task.Run(exportAction); // 核心执行点

                StatusText = successMsg; StatusColor = "#2ECC71";
                MessageBox.Show(successMsg, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = "导出失败"; StatusColor = "#E74C3C";
                MessageBox.Show($"导出失败：文件可能被占用或没有权限。\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}