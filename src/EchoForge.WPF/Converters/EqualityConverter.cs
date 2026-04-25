using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoForge.WPF.Converters;

/// <summary>
/// Converts a value to bool by checking equality with ConverterParameter.
/// Used for CheckBox binding to string enum-like properties (e.g., Filter == "blur").
/// </summary>
public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
            return parameter.ToString()!;
        return "none"; // Unchecked → reset to "none"
    }
}
