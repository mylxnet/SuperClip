using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SuperClip.ViewModels;

namespace SuperClip.Views
{
    // 序号：取元素在 ItemsSource 集合中的实际位置（IndexOf），
    // 直接基于当前显示顺序计算，避免依赖 AlternationIndex 在集合重建后不刷新的问题。
    [ValueConversion(typeof(object), typeof(int))]
    public class IndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] != null && values[1] is System.Collections.IList list)
                return list.IndexOf(values[0]) + 1;
            return 0;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // 过滤类型 → 中文
    public class FilterTypeConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            FilterType.Text => "文本",
            FilterType.TableCell => "表格",
            FilterType.Favorite => "收藏",
            _ => "全部"
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // 收藏状态 → ★/☆
    public class StarConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b && b ? "★" : "☆";
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // 空字符串 → Collapsed，否则 Visible
    public class EmptyToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // 空字符串 → Visible（用于搜索框占位提示），否则 Collapsed
    public class EmptyToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
