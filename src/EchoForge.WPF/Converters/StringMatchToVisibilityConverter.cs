using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoForge.WPF.Converters;

public class StringMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;

        var strValue = value.ToString()?.ToLowerInvariant();
        var strParam = parameter.ToString()?.ToLowerInvariant();

        return strValue == strParam ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
