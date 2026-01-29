using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nina.ManualFocuser.Converters
{
    public class BoolTrueVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }

    public class BoolFalseVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // null => false 취급 (✕ 아이콘이 보이게)
            if (value is bool b)
                return !b ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Visible;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }
}