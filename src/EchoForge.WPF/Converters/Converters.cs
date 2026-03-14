using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoForge.WPF.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        var targetStatus = parameter?.ToString() ?? "";
        
        // If parameter is multiple statuses separated by comma (e.g. "Completed,Failed")
        var targets = targetStatus.Split(',');
        return targets.Contains(status) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToProgressConverter : IValueConverter
{
    public static readonly StatusToProgressConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Created" =>          "1️⃣ Audio Analysis ⬜\n2️⃣ Image Generation ⬜\n3️⃣ Video Composition ⬜\n4️⃣ SEO Generation ⬜\n5️⃣ Awaiting Approval ⬜",
            "AnalyzingAudio" =>   "1️⃣ Audio Analysis 🔄\n2️⃣ Image Generation ⬜\n3️⃣ Video Composition ⬜\n4️⃣ SEO Generation ⬜\n5️⃣ Awaiting Approval ⬜",
            "GeneratingImages" => "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation 🔄\n3️⃣ Video Composition ⬜\n4️⃣ SEO Generation ⬜\n5️⃣ Awaiting Approval ⬜",
            "ComposingVideo" =>   "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation ✅\n3️⃣ Video Composition 🔄\n4️⃣ SEO Generation ⬜\n5️⃣ Awaiting Approval ⬜",
            "GeneratingSEO" =>    "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation ✅\n3️⃣ Video Composition ✅\n4️⃣ SEO Generation 🔄\n5️⃣ Awaiting Approval ⬜",
            "AwaitingApproval" => "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation ✅\n3️⃣ Video Composition ✅\n4️⃣ SEO Generation ✅\n5️⃣ Awaiting Approval ✅",
            "Uploading" =>        "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation ✅\n3️⃣ Video Composition ✅\n4️⃣ SEO Generation ✅\n5️⃣ Uploading to YouTube 🔄",
            "Completed" =>        "1️⃣ Audio Analysis ✅\n2️⃣ Image Generation ✅\n3️⃣ Video Composition ✅\n4️⃣ SEO Generation ✅\n5️⃣ YouTube Upload ✅\n✅ All Done!",
            "Failed" =>           "❌ Pipeline Failed — see error below",
            _ =>                  "⏳ Waiting..."
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverse = parameter?.ToString() == "Inverse";
        bool isNull = value == null;

        if (isInverse)
            return isNull ? Visibility.Visible : Visibility.Collapsed;

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts scene Duration (in seconds) to pixel width for the timeline.
/// Roughly 8px per second * zoomScale, clamped between 60-1000px.
/// Pass ZoomScale via ConverterParameter (defaults to 1.0).
/// </summary>
public class DurationToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double zoom = 1.0;
        if (parameter is double z) zoom = z;
        else if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) zoom = parsed;

        if (value is double duration)
        {
            double width = duration * 8 * zoom;
            if (width < 60) width = 60;
            if (width > 1000) width = 1000;
            return width;
        }
        return 140.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// MultiValueConverter: values[0] = Duration (double), values[1] = ZoomScale (double).
/// Returns pixel width = Duration * 8 * ZoomScale, clamped 60-1000px.
/// </summary>
public class DurationAndZoomMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double duration = 10;
        double zoom = 1.0;
        if (values.Length > 0 && values[0] is double d) duration = d;
        if (values.Length > 1 && values[1] is double z) zoom = z;

        double width = duration * 8 * zoom;
        if (width < 60) width = 60;
        if (width > 1000) width = 1000;
        return width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
