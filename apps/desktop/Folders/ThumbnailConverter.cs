using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Crafter.Desktop.Folders;

public sealed class ThumbnailConverter : IValueConverter
{
    public static readonly ThumbnailConverter Instance = new();

    private const int Width = 48;

    private const int Kept = 256;

    private readonly Dictionary<string, Bitmap?> decoded = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string file)
        {
            return null;
        }

        if (!decoded.TryGetValue(file, out var thumbnail))
        {
            if (decoded.Count >= Kept)
            {
                decoded.Clear();
            }

            thumbnail = decoded[file] = Decode(file);
        }

        return thumbnail;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static Bitmap? Decode(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);

            return Bitmap.DecodeToWidth(stream, Width);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
