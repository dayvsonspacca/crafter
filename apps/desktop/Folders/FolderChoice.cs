using Crafter.Desktop.Projects;

namespace Crafter.Desktop.Folders;

public sealed class FolderChoice(
    string folder,
    IReadOnlyList<Piece> name,
    IReadOnlyList<Piece> path,
    bool isRepository,
    bool isAdded) : PickerEntry
{
    public string Folder { get; } = folder;

    public IReadOnlyList<Piece> Name { get; } = name;

    public IReadOnlyList<Piece> Path { get; } = path;

    public bool HasPath => Path.Count > 0;

    public bool IsRepository { get; } = isRepository;

    public bool IsAdded { get; } = isAdded;

    public string Tag => IsAdded ? "added" : IsRepository && !HasPath ? "git" : string.Empty;

    public bool HasTag => Tag.Length > 0;
}
