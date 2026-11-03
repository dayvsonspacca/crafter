using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Folders;

public sealed class FolderChoice(
    string folder,
    IReadOnlyList<Piece> name,
    bool isRepository,
    bool isFound,
    bool isAdded) : PickerEntry
{
    public string Folder { get; } = folder;

    public IReadOnlyList<Piece> Name { get; } = name;

    public bool IsRepository { get; } = isRepository;

    public bool IsFound { get; } = isFound;

    public bool IsAdded { get; } = isAdded;

    public string Tag => IsAdded ? "added" : IsRepository && !IsFound ? "git" : string.Empty;

    public bool HasTag => Tag.Length > 0;
}
