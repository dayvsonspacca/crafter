using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed class PictureConverter : IValueConverter
{
    public static readonly PictureConverter Instance = new();

    private const int Width = 160;

    private readonly ConditionalWeakTable<Picture, Bitmap?> decoded = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Picture picture ? decoded.GetValue(picture, Decode) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static Bitmap? Decode(Picture picture)
    {
        try
        {
            using var stream = new MemoryStream(picture.Data);

            return Bitmap.DecodeToWidth(stream, Width);
        }
        catch (Exception unreadable) when (unreadable is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
