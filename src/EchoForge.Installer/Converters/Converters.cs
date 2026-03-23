using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoForge.Installer.Converters
{
    public class PercentageToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // To display progress as Grid Star sizing: 
                // Col0 = percentage*, Col1 = (100-percentage)*
                // But in our XAML we bound width to percentage* so returning GridLength is enough for column 1 alone, 
                // wait, GridLength for Star is new GridLength(val, GridUnitType.Star)
                return new GridLength(percentage, GridUnitType.Star);
            }
            return new GridLength(0, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
