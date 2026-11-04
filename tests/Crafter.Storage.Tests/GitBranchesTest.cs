using Crafter.Core.Projects;
using Crafter.Storage;

namespace Crafter.Storage.Tests;

public sealed class GitBranchesTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-git-").FullName;

    private readonly GitBranches branches = new();

    [Fact]
    public void ReadsTheBranchOfAClone()
    {
        Head(".git", "ref: refs/heads/dayvsonspacca/limpeza/remover-evoluir\n");

        Assert.Equal("dayvsonspacca/limpeza/remover-evoluir", branches.Of(new Project(folder)));
    }

    [Fact]
    public void FindsTheRepositoryAboveAProjectInsideIt()
    {
        Head(".git", "ref: refs/heads/main\n");
        var inner = Directory.CreateDirectory(Path.Combine(folder, "src", "app")).FullName;

        Assert.Equal("main", branches.Of(new Project(inner)));
    }

    [Fact]
    public void ShowsADetachedHeadAsItsShortSha()
    {
        Head(".git", "3f2a9c41e0b7d6a5c4b3a2f1e0d9c8b7a6f5e4d3\n");

        Assert.Equal("3f2a9c4", branches.Of(new Project(folder)));
    }

    [Fact]
    public void FollowsAWorktreeToItsOwnHead()
    {
        Head(Path.Combine("main", ".git", "worktrees", "feature"), "ref: refs/heads/feature/status-bar\n");
        var worktree = Directory.CreateDirectory(Path.Combine(folder, "feature")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: ../main/.git/worktrees/feature\n");

        Assert.Equal("feature/status-bar", branches.Of(new Project(worktree)));
    }

    [Fact]
    public void HasNoBranchOutsideARepository()
    {
        Assert.Null(branches.Of(new Project(folder)));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private void Head(string gitFolder, string content)
    {
        var git = Directory.CreateDirectory(Path.Combine(folder, gitFolder)).FullName;
        File.WriteAllText(Path.Combine(git, "HEAD"), content);
    }
}
