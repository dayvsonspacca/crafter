namespace Crafter.Core.Library;

// The skills and agents kept in one repository, mirrored on this machine.
public interface ILibrary
{
    // Brings the local copy to what the repository holds now, cloning it the first time.
    Task<SyncOutcome> Sync(string repository, CancellationToken stop = default);

    LibraryContents Read();
}
