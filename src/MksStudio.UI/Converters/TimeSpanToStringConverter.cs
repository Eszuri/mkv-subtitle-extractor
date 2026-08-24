using System.Globalization;
using System.Windows.Data;

namespace MksStudio.UI.Converters;

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            int hours = (int)ts.TotalHours;
            return $"{hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
        return "00:00:00.000";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && TimeSpan.TryParse(s, out var ts))
        {
            return ts;
        }
        return TimeSpan.Zero;
    }
}
