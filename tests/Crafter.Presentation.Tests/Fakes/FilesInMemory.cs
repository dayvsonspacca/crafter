using Crafter.Presentation.Conversations.Composing;

namespace Crafter.Presentation.Tests.Fakes;

internal sealed class FilesInMemory(params string[] paths) : IProjectFiles
{
    public string Folder => "/home/hero/crafter";

    public Task<IReadOnlyList<string>> List(CancellationToken stop = default) => Task.FromResult<IReadOnlyList<string>>(paths);
}
