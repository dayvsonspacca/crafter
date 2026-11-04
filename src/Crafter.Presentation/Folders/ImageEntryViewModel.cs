using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Folders;

public sealed class ImageEntryViewModel(string file, IReadOnlyList<NamePart> name) : PickerEntryViewModel
{
    public static readonly IReadOnlySet<string> Formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public string File { get; } = file;

    public IReadOnlyList<NamePart> Name { get; } = name;

    public string Format => System.IO.Path.GetExtension(File).TrimStart('.').ToUpperInvariant();
}
