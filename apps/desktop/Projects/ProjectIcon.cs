using Crafter.Core.Projects;

namespace Crafter.Desktop.Projects;

public static class ProjectIcon
{
    public static string? FileOf(Project project) => project.IconFile is { } file && File.Exists(file) ? file : null;

    public static string Letter(Project project) => project.Name.Text[..1].ToUpperInvariant();
}
