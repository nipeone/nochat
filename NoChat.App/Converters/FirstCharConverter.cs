using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NoChat.App.Converters;

public class FirstCharConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.Length > 0)
            return char.ToUpperInvariant(s[0]).ToString();
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
