using Crafter.Storage.Folders;

namespace Crafter.Storage.Tests.Folders;

public sealed class FolderListingTest
{
    [Fact]
    public void ListsTheVisibleSubfoldersByName()
    {
        using var folders = new TemporaryFolders();
        var reborn = folders.Make("reborn");
        var beacon = folders.Make("Beacon");
        folders.Make(".cache");
        File.WriteAllText(Path.Combine(folders.Root, "notes.md"), "not a folder");

        Assert.Equal([beacon, reborn], FolderListing.Children(folders.Root));
    }

    [Fact]
    public void ListsNothingInAFolderThatDoesNotExist()
    {
        Assert.Empty(FolderListing.Children("/nonexistent/crafter-folder"));
    }

    [Fact]
    public void KnowsARepositoryByItsGitFolder()
    {
        using var folders = new TemporaryFolders();

        Assert.True(FolderListing.IsRepository(folders.Repository("beacon")));
        Assert.False(FolderListing.IsRepository(folders.Make("notes")));
    }

    [Fact]
    public void KnowsAWorktreeByItsGitFile()
    {
        using var folders = new TemporaryFolders();
        var worktree = folders.Make("worktree");
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: ../beacon/.git/worktrees/worktree");

        Assert.True(FolderListing.IsRepository(worktree));
    }
}
