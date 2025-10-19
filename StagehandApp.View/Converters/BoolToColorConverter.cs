using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace StagehandApp.Views.Converters;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter TrueToGreenFalseToRed = new()
    {
        TrueColor = Colors.Green,
        FalseColor = Colors.Red
    };

    public Color TrueColor { get; set; } = Colors.Green;
    public Color FalseColor { get; set; } = Colors.Red;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}