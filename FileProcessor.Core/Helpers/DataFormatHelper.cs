using System;

namespace FileProcessor.Core.Helpers
{
    /// <summary>
    /// 工程数据公共清洗工具
    /// </summary>
    public static class DataFormatHelper
    {
        /// <summary>
        /// 深度清洗并解析工程数值（处理空白符与科学计数法）
        /// </summary>
        public static object ParseEngineNumber(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;

            // 移除首尾空格、移除 Windows 常见的非换行空格 (\u00A0)
            string cleanVal = val.Trim().Replace("\u00A0", "").Replace(" ", "");

            if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any,
                               System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }
            return val;
        }
    }
}