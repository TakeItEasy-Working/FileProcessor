using System.IO;
using System.Text.RegularExpressions;

namespace FileProcessor.Core.Helpers
{
    /// <summary>
    /// 文件与路径安全处理工具
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// 清洗 Windows 文件名中的非法字符，替换为下划线
        /// </summary>
        public static string GetSafeFileName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Data";
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            return Regex.Replace(input, string.Format(@"[{0}]", invalidChars), "_");
        }

        /// <summary>
        /// 清洗 Excel Sheet 名称（去除特殊符号，并限制最大长度）
        /// </summary>
        public static string GetSafeSheetName(string? input, int maxLength = 31)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Sheet";
            // Excel Sheet 名不可包含 : \ / ? * [ ]
            string clean = Regex.Replace(input, @"[:\\/?*\[\]]", "_");
            return clean.Length > maxLength ? clean.Substring(0, maxLength) : clean;
        }
    }
}