using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NoChat.App.Converters;

public class DateTimeToLocalStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime().ToString("HH:mm") : dt.ToString("HH:mm");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
