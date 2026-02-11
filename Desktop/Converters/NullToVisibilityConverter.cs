using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Desktop.Converters
{
    /// <summary>
    /// 将对象是否为 Null 转换为 Visibility 枚举值。
    /// 常用于根据数据是否存在来显示/隐藏 UI 元素。
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 如果对象不为 null，返回 Visible；否则返回 Collapsed。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果 parameter 传入 "Inverted"，则逻辑反转
            bool isInverted = parameter?.ToString() == "Inverted";

            bool isNull = value == null;

            if (isInverted)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}