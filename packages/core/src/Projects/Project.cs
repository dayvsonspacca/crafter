namespace Crafter.Core.Projects;

public sealed record Project
{
    public Project(string folder, ProjectName? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        Name = name ?? ProjectName.Of(Folder);
    }

    public string Folder { get; }

    public ProjectName Name { get; }

    public bool Equals(Project? other) => other is not null && Folder == other.Folder;

    public override int GetHashCode() => Folder.GetHashCode(StringComparison.Ordinal);
}
