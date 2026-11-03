using Avalonia.Media.Imaging;
using Crafter.Core.Projects;

namespace Crafter.Desktop.Projects;

public static class ProjectIcon
{
    public static Bitmap? Of(Project project) => Load(project.IconFile);

    public static Bitmap? Load(string? file)
    {
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        try
        {
            return new Bitmap(file);
        }
        catch (Exception unreadable) when (unreadable is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public static string Letter(Project project) => project.Name.Text[..1].ToUpperInvariant();
}
