using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Crafter.Desktop.Themes;

public sealed class HexBrushConverter : IValueConverter
{
    public static readonly HexBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is string hex ? Brush.Parse(hex) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
