using CommunityToolkit.Mvvm.ComponentModel;
using FileProcessor.Core.Models;
using FileProcessor.Desktop.Models;
using FileProcessor.Mediator;
using FileProcessor.Mediator.Models;
// --- 引入 LiveCharts 0.9.7 经典版命名空间 ---
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
// 强行指定 ColumnDefinition 的归属，防止与 WPF 控件冲突
using ColumnDefinition = FileProcessor.Core.Models.ColumnDefinition;

namespace FileProcessor.Desktop.ViewModels
{
    public partial class SlotViewModel : ObservableObject
    {
        private readonly DataCoordinator _coordinator;
        private readonly int _index;

        private bool _isRestoringState = false;
        private bool _isUpdatingColumns = false;
        private ProcessedResult? _currentRawData;

        /// <summary>
        /// 独立于具体数据版本的插槽显示状态记忆对象
        /// </summary>
        private readonly SlotDisplayState _displayState = new SlotDisplayState();

        public SlotViewModel(DataCoordinator coordinator, int index)
        {
            _coordinator = coordinator;
            _index = index;

            _coordinator.SlotDataChanged += (idx, data) => {
                if (idx == _index)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        _currentRawData = data;
                        Status = data != null ? SlotStatus.Ready : SlotStatus.NoData;
                        InitializeColumns();
                    });
                }
            };
        }

        // ==========================================
        // 1. 基础状态
        // ==========================================
        [ObservableProperty] private string? _selectedFile;
        [ObservableProperty] private string? _selectedBlock;
        [ObservableProperty] private SlotStatus _status = SlotStatus.Empty;

        public ObservableCollection<string> AvailableFiles { get; } = new();
        public ObservableCollection<string> AvailableBlocks { get; } = new();

        // ==========================================
        // 2. 视图切换与动态列
        // ==========================================
        [ObservableProperty] private bool _isChartMode = false;
        [ObservableProperty] private DataTable? _displayTable;

        public ObservableCollection<ColumnDefinition> AvailableColumns { get; } = new();

        // --- 表格模式 4 列 ---
        [ObservableProperty] private ColumnDefinition? _tableCol1;
        [ObservableProperty] private ColumnDefinition? _tableCol2;
        [ObservableProperty] private ColumnDefinition? _tableCol3;
        [ObservableProperty] private ColumnDefinition? _tableCol4;

        // --- 图表模式 XY 轴 ---
        [ObservableProperty] private ColumnDefinition? _chartXCol;
        [ObservableProperty] private ColumnDefinition? _chartYCol;

        // --- LiveCharts 0.9.7 专属属性 ---
        [ObservableProperty] private SeriesCollection? _chartSeries;
        [ObservableProperty] private string _xAxisTitle = "";
        [ObservableProperty] private string _yAxisTitle = "";


        // ==========================================
        // 5. 核心拦截与智能转换逻辑
        // ==========================================
        private void InitializeColumns()
        {
            if (_currentRawData?.Columns == null || _currentRawData.Columns.Count == 0)
            {
                AvailableColumns.Clear();
                DisplayTable = null;
                ChartSeries = null;
                return;
            }

            _isUpdatingColumns = true;
            try
            {
                AvailableColumns.Clear();
                foreach (var col in _currentRawData.Columns)
                {
                    AvailableColumns.Add(col);
                }

                var floorCol = AvailableColumns.FirstOrDefault(c => c.Header.Contains("层号") || c.Key.Contains("Floor"));
                var towerCol = AvailableColumns.FirstOrDefault(c => c.Header.Contains("塔号") || c.Key.Contains("Tower"));
                var dataCols = AvailableColumns.Where(c => c != floorCol && c != towerCol).ToList();

                // 【微调点1】：尝试寻找上一版记忆的列是否存在于当前可用列中
                var memT1 = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.TableCol1Key);
                var memT2 = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.TableCol2Key);
                var memT3 = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.TableCol3Key);
                var memT4 = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.TableCol4Key);
                var memCX = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.ChartXColKey);
                var memCY = AvailableColumns.FirstOrDefault(c => c.Key == _displayState.ChartYColKey);

                // 【微调点2】：记忆优先 (memXX ??)，如果不存在则直接跳过，丝滑退回原有的编程逻辑
                TableCol1 = memT1 ?? floorCol ?? (AvailableColumns.Count > 0 ? AvailableColumns[0] : null);
                TableCol2 = memT2 ?? towerCol ?? (AvailableColumns.Count > 1 ? AvailableColumns[1] : null);
                TableCol3 = memT3 ?? (dataCols.Count > 0 ? dataCols[0] : (AvailableColumns.Count > 2 ? AvailableColumns[2] : null));
                TableCol4 = memT4 ?? (dataCols.Count > 1 ? dataCols[1] : (AvailableColumns.Count > 3 ? AvailableColumns[3] : null));

                ChartYCol = memCY ?? floorCol ?? (AvailableColumns.Count > 0 ? AvailableColumns[0] : null);
                ChartXCol = memCX ?? (dataCols.Count > 0 ? dataCols[0] : (AvailableColumns.Count > 1 ? AvailableColumns[1] : null));
            }
            finally
            {
                _isUpdatingColumns = false;
                BuildDisplayTable();
                BuildChartSeries();
            }
        }

        /// <summary>
        /// 构建动态的 4 列 DataTable (重构为适配自定义表头的固定列名机制)
        /// </summary>
        private void BuildDisplayTable()
        {
            if (_currentRawData == null || _isUpdatingColumns) return;

            var dt = new DataTable();
            var selectedCols = new[] { TableCol1, TableCol2, TableCol3, TableCol4 };

            // 1. 构建固定的内部列名，方便 XAML 稳定绑定
            for (int i = 0; i < 4; i++)
            {
                dt.Columns.Add(new DataColumn($"Col{i}"));
            }

            // 2. 映射投影数据
            foreach (var rowDict in _currentRawData.Rows)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < 4; i++)
                {
                    var colDef = selectedCols[i];
                    // 如果这列用户没选(null)，或者底层没数据，就填空字符串
                    dr[$"Col{i}"] = (colDef != null && rowDict.TryGetValue(colDef.Key, out var val)) ? val : string.Empty;
                }
                dt.Rows.Add(dr);
            }

            DisplayTable = dt;
        }

        /// <summary>
        /// 构建 0.9.7 版本的图表数据
        /// </summary>
        private void BuildChartSeries()
        {
            if (_currentRawData == null || _isUpdatingColumns || ChartXCol == null || ChartYCol == null) return;

            // 0.9.7 使用 ChartValues 容器
            var values = new ChartValues<ObservablePoint>();

            foreach (var rowDict in _currentRawData.Rows)
            {
                if (rowDict.TryGetValue(ChartXCol.Key, out string? xStr) &&
                    rowDict.TryGetValue(ChartYCol.Key, out string? yStr))
                {
                    if (double.TryParse(xStr, out double xVal) && double.TryParse(yStr, out double yVal))
                    {
                        values.Add(new ObservablePoint(xVal, yVal));
                    }
                }
            }

            // 更新 0.9.7 的 SeriesCollection
            ChartSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = ChartXCol.Header, // 图例名称
                    Values = values,
                    PointGeometrySize = 8,    // 数据点大小
                    LineSmoothness = 0        // 0代表直线，适合工程类数据
                }
            };

            // 简单直接的字符串绑定坐标轴名称
            XAxisTitle = ChartXCol.Header;
            YAxisTitle = ChartYCol.Header;
        }

        // 【微调点3】：展开属性改变的钩子，保存用户的选择。
        // 使用 !_isUpdatingColumns 判断，防止程序初始化列时的自动赋值冲刷掉用户的真实记忆
        partial void OnTableCol1Changed(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.TableCol1Key = value.Key;
            BuildDisplayTable();
        }

        partial void OnTableCol2Changed(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.TableCol2Key = value.Key;
            BuildDisplayTable();
        }

        partial void OnTableCol3Changed(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.TableCol3Key = value.Key;
            BuildDisplayTable();
        }

        partial void OnTableCol4Changed(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.TableCol4Key = value.Key;
            BuildDisplayTable();
        }

        partial void OnChartXColChanged(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.ChartXColKey = value.Key;
            BuildChartSeries();
        }

        partial void OnChartYColChanged(ColumnDefinition? value)
        {
            if (!_isUpdatingColumns && value != null) _displayState.ChartYColKey = value.Key;
            BuildChartSeries();
        }

        public void RefreshFileList()
        {
            _isRestoringState = true;
            try
            {
                string? cachedFile = SelectedFile;
                string? cachedBlock = SelectedBlock;

                SelectedFile = null;
                SelectedBlock = null;

                AvailableFiles.Clear();
                foreach (var f in _coordinator.GetAvailableFiles())
                {
                    AvailableFiles.Add(f);
                }

                if (!string.IsNullOrEmpty(cachedFile) && AvailableFiles.Contains(cachedFile))
                {
                    SelectedFile = cachedFile;
                    AvailableBlocks.Clear();
                    foreach (var b in _coordinator.GetBlocksForFile(cachedFile))
                    {
                        AvailableBlocks.Add(b);
                    }

                    if (!string.IsNullOrEmpty(cachedBlock) && AvailableBlocks.Contains(cachedBlock))
                    {
                        SelectedBlock = cachedBlock;
                    }
                }
                else
                {
                    AvailableBlocks.Clear();
                }
            }
            finally
            {
                _isRestoringState = false;
            }
        }

        partial void OnSelectedFileChanged(string? value)
        {
            if (_isRestoringState) return;
            AvailableBlocks.Clear();
            SelectedBlock = null;
            if (string.IsNullOrEmpty(value)) return;
            foreach (var b in _coordinator.GetBlocksForFile(value))
            {
                AvailableBlocks.Add(b);
            }
        }

        partial void OnSelectedBlockChanged(string? value)
        {
            if (!string.IsNullOrEmpty(SelectedFile) && !string.IsNullOrEmpty(value))
            {
                _coordinator.ConfigureSlot(_index, SelectedFile, value);
            }
        }
    }
}