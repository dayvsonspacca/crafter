using Crafter.Core.Library;

namespace Crafter.Presentation.Tests.Fakes;

internal sealed class LibraryInMemory : ILibrary
{
    public List<string> Synced { get; } = [];

    public SyncOutcome Outcome { get; set; } = new Synced("a1b2c3d");

    public LibraryContents Contents { get; set; } = LibraryContents.Empty;

    public Task<SyncOutcome> Sync(string repository, CancellationToken stop = default)
    {
        Synced.Add(repository);

        return Task.FromResult(Outcome);
    }

    public LibraryContents Read() => Contents;
}
