using Crafter.Storage.Folders;

namespace Crafter.Presentation.Conversations.Composing;

public sealed class FilesOnDisk(string folder) : IProjectFiles
{
    public string Folder { get; } = folder;

    public Task<IReadOnlyList<string>> List(CancellationToken stop = default) => Task.Run(() => ProjectFiles.Of(Folder, stop), stop);
}
