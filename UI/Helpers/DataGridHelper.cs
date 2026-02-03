using FileProcessor.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UI.Helpers
{
    public static class DataGridHelper
    {
        public static readonly DependencyProperty BindableColumnsProperty =
            DependencyProperty.RegisterAttached("BindableColumns", typeof(ProcessedResult),
                typeof(DataGridHelper), new PropertyMetadata(null, OnBindableColumnsChanged));

        public static void SetBindableColumns(DependencyObject element, ProcessedResult value)
            => element.SetValue(BindableColumnsProperty, value);

        public static ProcessedResult GetBindableColumns(DependencyObject element)
            => (ProcessedResult)element.GetValue(BindableColumnsProperty);

        private static void OnBindableColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                dataGrid.Columns.Clear();
                dataGrid.ItemsSource = null;

                if (e.NewValue is ProcessedResult result && result.Columns != null)
                {
                    dataGrid.AutoGenerateColumns = false;

                    foreach (var col in result.Columns)
                    {
                        dataGrid.Columns.Add(new DataGridTextColumn
                        {
                            // 【修正点】确保 col.Header 与 ColumnDefinition 中的属性名一致
                            Header = col.HeaderText,

                            Binding = new Binding($"[{col.Key}]"),

                            ElementStyle = col.IsNumeric ? CreateRightAlignedStyle() : null
                        });
                    }

                    dataGrid.ItemsSource = result.Rows;
                }
            }
        }

        private static Style CreateRightAlignedStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            return style;
        }
    }
}