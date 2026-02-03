using FileProcessor.Core.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UI.Helpers
{
    /// <summary>
    /// DataGrid 辅助类：通过附加属性实现列的动态绑定和自动生成。
    /// 解决了 WPF 原生 DataGrid 无法直接绑定列集合的问题。
    /// </summary>
    public static class DataGridHelper
    {
        /// <summary>
        /// 定义 BindableColumns 附加属性。
        /// 当绑定的 ProcessedResult 发生变化时，自动重新构建 DataGrid 的列结构。
        /// </summary>
        public static readonly DependencyProperty BindableColumnsProperty =
            DependencyProperty.RegisterAttached(
                "BindableColumns",
                typeof(object), // 使用 object 以提高 XAML 绑定的兼容性
                typeof(DataGridHelper),
                new PropertyMetadata(null, OnBindableColumnsChanged));

        /// <summary>
        /// 设置 BindableColumns 的值
        /// </summary>
        public static void SetBindableColumns(DependencyObject element, object value)
            => element.SetValue(BindableColumnsProperty, value);

        /// <summary>
        /// 获取 BindableColumns 的值
        /// </summary>
        public static object GetBindableColumns(DependencyObject element)
            => element.GetValue(BindableColumnsProperty);

        private static void OnBindableColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            // 1. 清理旧数据
            dataGrid.Columns.Clear();
            dataGrid.ItemsSource = null;

            // 2. 验证新值是否为有效的解析结果
            if (e.NewValue is not ProcessedResult result) return;

            // 3. 锁定自动生成，开始手动构建列
            dataGrid.AutoGenerateColumns = false;

            if (result.Columns != null)
            {
                foreach (var col in result.Columns)
                {
                    // 构建文本列
                    var textColumn = new DataGridTextColumn
                    {
                        Header = col.HeaderText,
                        // 使用索引器绑定：Rows 是 List<Dictionary<string, string>>
                        // 绑定路径格式为 [Key]
                        Binding = new Binding($"[{col.Key}]")
                        {
                            Mode = BindingMode.OneWay,
                            FallbackValue = string.Empty
                        },
                        // 如果是数值类型，应用右对齐样式
                        ElementStyle = col.IsNumeric ? CreateRightAlignedStyle() : null,
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star) // 平铺列宽
                    };

                    dataGrid.Columns.Add(textColumn);
                }
            }

            // 4. 绑定行数据源
            dataGrid.ItemsSource = result.Rows;
        }

        /// <summary>
        /// 创建单元格右对齐样式（适用于数值展示）
        /// </summary>
        private static Style CreateRightAlignedStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            // 增加一点内边距，美观考虑
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 0, 5, 0)));
            return style;
        }
    }
}