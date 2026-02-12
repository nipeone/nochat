using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NoChat.App.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? new SolidColorBrush(Color.Parse("#5B8DEE")) : new SolidColorBrush(Color.Parse("#252B38"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
