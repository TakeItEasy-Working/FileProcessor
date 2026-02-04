using FileProcessor.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UI.Helpers
{
    /// <summary>
    /// DataGrid 辅助类：解决动态表头绑定问题并优化列显示策略。
    /// </summary>
    public static class DataGridHelper
    {
        public static readonly DependencyProperty BindableColumnsProperty =
            DependencyProperty.RegisterAttached(
                "BindableColumns",
                typeof(object),
                typeof(DataGridHelper),
                new PropertyMetadata(null, OnBindableColumnsChanged));

        public static void SetBindableColumns(DependencyObject element, object value) => element.SetValue(BindableColumnsProperty, value);
        public static object GetBindableColumns(DependencyObject element) => element.GetValue(BindableColumnsProperty);

        private static void OnBindableColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            dataGrid.Columns.Clear();
            dataGrid.ItemsSource = null;

            if (e.NewValue is not ProcessedResult result) return;

            dataGrid.AutoGenerateColumns = false;

            if (result.Columns != null)
            {
                foreach (var col in result.Columns)
                {
                    var textColumn = new DataGridTextColumn
                    {
                        Header = col.HeaderText,
                        Binding = new Binding($"[{col.Key}]") { Mode = BindingMode.OneWay },

                        // 优化点：使用 Auto 模式让列宽随标题或内容自适应
                        // 同时设置 MinWidth 确保即使标题很短，数据也有足够的展示空间
                        Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                        MinWidth = 80
                    };

                    // 针对数字列，设置右对齐样式
                    if (col.IsNumeric)
                    {
                        var style = new Style(typeof(TextBlock));
                        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 0, 5, 0)));
                        textColumn.ElementStyle = style;
                    }

                    dataGrid.Columns.Add(textColumn);
                }
            }
            dataGrid.ItemsSource = result.Rows;
        }
    }
}