using FileProcessor.Core.Models;
using System.Data;

namespace FileProcessor.Desktop.Helpers
{

    public static class ResultExtensions
    {
        /// <summary>
        /// 将 ProcessedResult 转换为 DataTable。
        /// 解决 ColumnDefinition 无法直接转换的问题。
        /// </summary>
        public static DataTable ToDataTable(this ProcessedResult result)
        {
            var dt = new DataTable();

            // 安全检查：如果列或行数据为空，返回空表
            if (result.Columns == null || result.Rows == null) return dt;

            // 1. 添加列定义
            foreach (var colDef in result.Columns)
            {
                // 使用 Key 存储，Header 作为 Caption (DataGrid 默认会读取 Caption 作为列头)
                DataColumn dc = new DataColumn(colDef.Key)
                {
                    Caption = colDef.Header
                };
                dt.Columns.Add(dc);
            }

            // 2. 填充行数据
            foreach (var rowDict in result.Rows)
            {
                var dr = dt.NewRow();
                foreach (var colDef in result.Columns)
                {
                    // 使用 colDef.Key 从字典中取值
                    if (rowDict.TryGetValue(colDef.Key, out var value))
                    {
                        dr[colDef.Key] = value;
                    }
                    else
                    {
                        dr[colDef.Key] = string.Empty;
                    }
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }
    }
}