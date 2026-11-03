using Crafter.Core.Projects;
using Crafter.Desktop.Keyboard;

namespace Crafter.Desktop.Projects;

public sealed class Pick
{
    public Pick(Found found, Shortcut? shortcut)
    {
        var folder = new FolderLabel(found.Project.Folder);

        Project = found.Project;
        Name = Piece.Split(found.Project.Name, found.NameHits);
        Folder = Piece.Split(folder.Text, found.FolderHits.Select(at => at - folder.Shift).Where(at => at >= 0));
        Shortcut = shortcut?.ToString() ?? string.Empty;
    }

    public Project Project { get; }

    public IReadOnlyList<Piece> Name { get; }

    public IReadOnlyList<Piece> Folder { get; }

    public string Shortcut { get; }
}
