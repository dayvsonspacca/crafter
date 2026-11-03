using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Crafter.Desktop.Projects;

public sealed class IconFileConverter : IValueConverter
{
    public static readonly IconFileConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is string file ? Load(file) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static Bitmap? Load(string file)
    {
        try
        {
            return new Bitmap(file);
        }
        catch (Exception unreadable) when (unreadable is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
