using Crafter.Storage.Folders;

namespace Crafter.Storage.Tests.Folders;

public sealed class RepositoryFinderTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public void FindsRepositoriesUnderTheRoot()
    {
        using var folders = new TemporaryFolders();
        var beacon = folders.Repository("me/beacon");
        var crafter = folders.Repository("me/desktop/crafter");
        folders.Make("me/notes");

        Assert.Equal([beacon, crafter], new RepositoryFinder(4).Find(folders.Root, Stop).Order());
    }

    [Fact]
    public void StopsAtTheDepth()
    {
        using var folders = new TemporaryFolders();
        var near = folders.Repository("me/beacon");
        folders.Repository("me/desktop/crafter");

        Assert.Equal([near], new RepositoryFinder(2).Find(folders.Root, Stop));
    }

    [Fact]
    public void DoesNotLookInsideARepository()
    {
        using var folders = new TemporaryFolders();
        var beacon = folders.Repository("beacon");
        folders.Repository("beacon/packages/inner");

        Assert.Equal([beacon], new RepositoryFinder(4).Find(folders.Root, Stop));
    }

    [Theory]
    [InlineData("node_modules")]
    [InlineData("vendor")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".cache")]
    public void SkipsFoldersThatHoldNoProjects(string skipped)
    {
        using var folders = new TemporaryFolders();
        folders.Repository($"{skipped}/package");

        Assert.Empty(new RepositoryFinder(4).Find(folders.Root, Stop));
    }

    [Fact]
    public void GivesUpWhenStopped()
    {
        using var folders = new TemporaryFolders();
        folders.Repository("beacon");
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        Assert.Throws<OperationCanceledException>(() => new RepositoryFinder(4).Find(folders.Root, stop.Token).ToList());
    }
}
