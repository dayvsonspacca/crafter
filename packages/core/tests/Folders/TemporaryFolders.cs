namespace Crafter.Core.Tests.Folders;

internal sealed class TemporaryFolders : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("crafter-folders-").FullName;

    public string Make(string relative)
    {
        var folder = Path.Combine(Root, relative);
        Directory.CreateDirectory(folder);

        return folder;
    }

    public string Repository(string relative)
    {
        var folder = Make(relative);
        Directory.CreateDirectory(Path.Combine(folder, ".git"));

        return folder;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
