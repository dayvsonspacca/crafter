using Avalonia.Media.Imaging;
using Crafter.Desktop.Projects;

namespace Crafter.Desktop.Folders;

public sealed class ImageChoice(string file, IReadOnlyList<Piece> name) : PickerEntry
{
    private const int ThumbnailWidth = 48;

    private readonly Lazy<Bitmap?> thumbnail = new(() => Decode(file));

    public static readonly IReadOnlySet<string> Formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public string File { get; } = file;

    public IReadOnlyList<Piece> Name { get; } = name;

    public string Format => System.IO.Path.GetExtension(File).TrimStart('.').ToUpperInvariant();

    public Bitmap? Thumbnail => thumbnail.Value;

    private static Bitmap? Decode(string file)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(file);

            return Bitmap.DecodeToWidth(stream, ThumbnailWidth);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
