namespace Crafter.Core.Projects;

public sealed record Project
{
    public Project(string folder, ProjectName? name = null, string? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        Name = name ?? ProjectName.Of(Folder);
        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
    }

    public string Folder { get; }

    public ProjectName Name { get; }

    public string? Icon { get; }

    public string? IconFile => Icon is null ? null : Path.GetFullPath(Path.Combine(Folder, Icon));

    public bool Equals(Project? other) => other is not null && Folder == other.Folder;

    public override int GetHashCode() => Folder.GetHashCode(StringComparison.Ordinal);
}
