using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests;

public sealed class WorkbenchViewModelTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-workbench-").FullName;

    [Fact]
    public void ComesBackToTheChatThatWasInFront()
    {
        var workbench = Workbench();
        var crafter = Project("crafter");
        var beacon = Project("beacon");

        workbench.Open(crafter);
        workbench.NewChatCommand.Execute(null);
        workbench.NewChatCommand.Execute(null);
        var second = workbench.Desk!.Selected;
        workbench.Open(beacon);
        workbench.Open(crafter);

        Assert.Same(second, workbench.Desk!.Selected);
        Assert.False(workbench.Desk.ShowsOverview);
    }

    [Fact]
    public void OpensAProjectWithoutAChatOnItsOverview()
    {
        var workbench = Workbench();

        workbench.Open(Project("crafter"));

        Assert.True(workbench.Desk!.ShowsOverview);
    }

    [Fact]
    public void OpeningTheProjectInFrontShowsItsOverview()
    {
        var workbench = Workbench();
        var crafter = Project("crafter");
        workbench.Open(crafter);
        workbench.NewChatCommand.Execute(null);

        workbench.Open(crafter);

        Assert.True(workbench.Desk!.ShowsOverview);
    }

    [Fact]
    public void GoesToAChatByItsPlace()
    {
        var workbench = Workbench();
        workbench.Open(Project("crafter"));
        workbench.NewChatCommand.Execute(null);
        workbench.NewChatCommand.Execute(null);

        workbench.OpenChat(0);

        Assert.Same(workbench.Desk!.Chats[0], workbench.Desk.Selected);
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private Project Project(string name) => new(Directory.CreateDirectory(Path.Combine(home, name)).FullName);

    private WorkbenchViewModel Workbench()
    {
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        var clipboard = new ClipboardInMemory();
        var preferences = new PreferenceBook(new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id] }));
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);

        return new WorkbenchViewModel(
            bookshelf,
            preferences,
            picker,
            new ProjectNamerViewModel(picker),
            setup,
            new PreferencesViewModel(preferences, picker, setup, new ThemesInMemory()),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, time),
            clipboard,
            new StatusBarViewModel(new NoBranches(), time));
    }

    private sealed class NoBranches : IBranches
    {
        public string? Of(Project project) => null;
    }
}
