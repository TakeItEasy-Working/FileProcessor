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
            if (d is DataGrid dataGrid && e.NewValue is ProcessedResult result)
            {
                dataGrid.Columns.Clear();
                if (result.Columns == null) return;

                foreach (var col in result.Columns)
                {
                    // 注意：这里的 col 类型现在是 FileProcessor.Core.Models.ColumnDefinition
                    dataGrid.Columns.Add(new DataGridTextColumn
                    {
                        // 确保这里调用的是你的成员：Header 和 Key
                        Header = col.HeaderText,
                        Binding = new Binding($"[{col.Key}]")
                    });
                }

                dataGrid.ItemsSource = result.Rows;
            }
        }
    }
}
