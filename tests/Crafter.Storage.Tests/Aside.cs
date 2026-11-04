namespace Crafter.Storage.Tests;

internal static class Aside
{
    public static string Of(string file) =>
        Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + ".*.unreadable").Single();
}
