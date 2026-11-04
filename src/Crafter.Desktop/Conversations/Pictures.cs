using Avalonia;
using Avalonia.Media.Imaging;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public static class Pictures
{
    private const int Largest = 2000;

    private const int Heaviest = 3_500_000;

    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    public static bool IsPicture(string file) => MediaTypes.ContainsKey(Path.GetExtension(file));

    public static Picture? Of(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var scale = Math.Min(1.0, (double)Largest / Math.Max(size.Width, size.Height));
        using var scaled = scale < 1.0
            ? bitmap.CreateScaledBitmap(new PixelSize(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale))))
            : null;
        using var png = new MemoryStream();
        (scaled ?? bitmap).Save(png, new PngBitmapEncoderOptions());

        return png.Length > 0 ? new Picture("image/png", png.ToArray()) : null;
    }

    public static Picture? Of(string file)
    {
        if (!MediaTypes.TryGetValue(Path.GetExtension(file), out var mediaType))
        {
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(file);
            if (data.Length == 0)
            {
                return null;
            }

            if (data.Length <= Heaviest)
            {
                return new Picture(mediaType, data);
            }

            using var stream = new MemoryStream(data);
            using var bitmap = new Bitmap(stream);

            return Of(bitmap);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
