using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Folders;

public sealed class FolderEntry(
    string folder,
    IReadOnlyList<NamePart> name,
    bool isRepository,
    bool isFound,
    bool isAdded) : PickerEntry
{
    public string Folder { get; } = folder;

    public IReadOnlyList<NamePart> Name { get; } = name;

    public bool IsRepository { get; } = isRepository;

    public bool IsFound { get; } = isFound;

    public bool IsAdded { get; } = isAdded;

    public string Tag => IsAdded ? "added" : IsRepository && !IsFound ? "git" : string.Empty;

    public bool HasTag => Tag.Length > 0;
}
