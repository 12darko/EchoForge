using System.Globalization;
using System.Windows.Data;

namespace EchoForge.WPF.Converters;

public class RadioBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? checkValue = value?.ToString();
        string? targetValue = parameter?.ToString();
        return checkValue?.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(true) == true ? parameter?.ToString()! : Binding.DoNothing;
    }
}
