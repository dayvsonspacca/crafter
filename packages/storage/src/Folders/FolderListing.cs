namespace Crafter.Storage.Folders;

public static class FolderListing
{
    private static readonly EnumerationOptions Visible = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    public static IReadOnlyList<string> Children(string folder)
    {
        try
        {
            return
            [
                .. new DirectoryInfo(folder)
                    .EnumerateDirectories("*", Visible)
                    .Where(child => !child.Name.StartsWith('.'))
                    .Select(child => child.FullName)
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception unreachable) when (unreachable is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    public static bool IsRepository(string folder)
    {
        var git = Path.Combine(folder, ".git");

        return Directory.Exists(git) || File.Exists(git);
    }
}
