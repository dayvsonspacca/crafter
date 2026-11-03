namespace Crafter.Core.Projects;

public sealed record Project
{
    public Project(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
    }

    public string Folder { get; }

    public string Name => Path.GetFileName(Folder) is { Length: > 0 } name ? name : Folder;
}
