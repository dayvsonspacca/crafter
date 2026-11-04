using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Capabilities;

public sealed class ProjectSkillsTest : IDisposable
{
    private static readonly Skill Commit = new("commit", "Commit", "Writes commits the way this team does.", null, "/library/skills/commit");

    private static readonly Skill Migrations = new("laravel-migrations", "Laravel migrations", "Changes the database schema.", null, "/library/skills/laravel-migrations");

    private readonly string home = Directory.CreateTempSubdirectory("crafter-skills-").FullName;

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private readonly PreferencesInMemory saved;

    private readonly WorkbenchViewModel workbench;

    private readonly LibraryViewModel library;

    private readonly Project crafter;

    private readonly Project lorekeeper;

    public ProjectSkillsTest()
    {
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new ClipboardInMemory();
        saved = new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id], Library = "git@github.com:hero/crafter-library.git" });
        var preferences = new PreferenceBook(saved);
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        library = new LibraryViewModel(preferences, new LibraryInMemory { Contents = new LibraryContents([Commit, Migrations], []) }, time);
        workbench = new WorkbenchViewModel(
            new Project(Directory.CreateDirectory(Path.Combine(home, "home")).FullName, new ProjectName("Home")),
            bookshelf, preferences, picker, new ProjectNamerViewModel(picker), setup,
            new PreferencesViewModel(preferences, picker, setup, new ThemesInMemory(), library),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, time), clipboard,
            new StatusBarViewModel(new Branches(), time));

        crafter = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "crafter")).FullName, new ProjectName("Crafter"));
        lorekeeper = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "lorekeeper")).FullName, new ProjectName("Lorekeeper"));
        workbench.Open(crafter);
    }

    [Fact]
    public void ADefaultReachesEveryProjectAndIsThisCraftersChoice()
    {
        library.Entries.Single(entry => entry.Skill == Commit).IsDefault = true;

        Assert.Equal([Commit], workbench.SkillsFor(crafter));
        Assert.Equal([Commit], workbench.SkillsFor(lorekeeper));
        Assert.Equal(["commit"], saved.Saved.DefaultSkills);
        Assert.Equal("1 skill", workbench.Status.CapabilitiesLabel);
    }

    [Fact]
    public void AGivenSkillReachesOnlyItsProject()
    {
        workbench.Status.ShowCapabilitiesCommand.Execute(null);

        workbench.Capabilities.Skills.Single(choice => choice.Skill == Migrations).IsGiven = true;

        Assert.Equal([Migrations], workbench.SkillsFor(crafter));
        Assert.Empty(workbench.SkillsFor(lorekeeper));
        Assert.Equal("1 skill", workbench.Status.CapabilitiesLabel);
    }

    [Fact]
    public void TheCapabilitiesWindowOpensOnItsFirstTab()
    {
        workbench.Status.ShowCapabilitiesCommand.Execute(null);
        Assert.True(workbench.Capabilities.ShowsCapabilities);

        workbench.Capabilities.ShowSkillsTabCommand.Execute(null);
        Assert.True(workbench.Capabilities.ShowsSkills);
        Assert.False(workbench.Capabilities.ShowsCapabilities);

        workbench.Capabilities.Close();
        workbench.Status.ShowCapabilitiesCommand.Execute(null);
        Assert.True(workbench.Capabilities.ShowsCapabilities);
    }

    [Fact]
    public void ItsChatsOfferTheSkillsTheProjectGets()
    {
        library.MakeDefault(Commit, isDefault: true);
        workbench.NewChatCommand.Execute(null);

        var chat = workbench.Desk!.Selected!;
        chat.Mentions.Look("/", 1);

        Assert.Equal(["commit"], chat.Mentions.Choices.Select(choice => choice.Path));
    }

    [Fact]
    public void TheCapabilitiesWindowShowsADefaultTickedAndFixed()
    {
        library.MakeDefault(Commit, isDefault: true);

        workbench.Status.ShowCapabilitiesCommand.Execute(null);

        var commit = workbench.Capabilities.Skills.Single(choice => choice.Skill == Commit);
        Assert.True(commit.IsDefault);
        Assert.True(commit.IsGiven);
        Assert.False(commit.CanChange);
        Assert.True(library.Entries.Single(entry => entry.Skill == Commit).IsDefault);

        var migrations = workbench.Capabilities.Skills.Single(choice => choice.Skill == Migrations);
        Assert.False(migrations.IsGiven);
        Assert.True(migrations.CanChange);
    }

    [Fact]
    public void TakingADefaultBackLeavesWhatTheProjectWasGiven()
    {
        library.MakeDefault(Commit, isDefault: true);
        workbench.Give(crafter, Migrations, given: true);

        Assert.Equal([Commit, Migrations], workbench.SkillsFor(crafter));

        library.MakeDefault(Commit, isDefault: false);
        Assert.Equal([Migrations], workbench.SkillsFor(crafter));
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => null;
    }
}
