using Crafter.Core.Projects;
using Crafter.Presentation.Status;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Status;

public sealed class StatusBarViewModelTest
{
    private readonly FakeTimeProvider time = new();

    private readonly BranchesInMemory branches = new();

    [Fact]
    public void ShowsTheBranchOfTheWatchedProject()
    {
        using var status = new StatusBarViewModel(branches, time);
        branches.On["/home/hero/crafter"] = "main";

        status.Watch(new Project("/home/hero/crafter"));

        Assert.Equal("main", status.Branch);
        Assert.True(status.HasBranch);
    }

    [Fact]
    public void NoticesAnAgentSwitchingBranches()
    {
        using var status = new StatusBarViewModel(branches, time);
        branches.On["/home/hero/crafter"] = "main";
        status.Watch(new Project("/home/hero/crafter"));

        branches.On["/home/hero/crafter"] = "feature/status-bar";
        time.Advance(StatusBarViewModel.Every);

        Assert.Equal("feature/status-bar", status.Branch);
    }

    [Fact]
    public void ShowsNoBranchOutsideARepositoryOrWithoutAProject()
    {
        using var status = new StatusBarViewModel(branches, time);

        status.Watch(new Project("/home/hero/notes"));
        Assert.False(status.HasBranch);

        branches.On["/home/hero/crafter"] = "main";
        status.Watch(new Project("/home/hero/crafter"));
        status.Watch(null);
        Assert.Null(status.Branch);
    }

    private sealed class BranchesInMemory : IBranches
    {
        public Dictionary<string, string> On { get; } = [];

        public string? Of(Project project) => On.GetValueOrDefault(project.Folder);
    }
}
