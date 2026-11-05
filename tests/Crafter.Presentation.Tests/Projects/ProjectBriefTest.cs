using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Projects;

public sealed class ProjectBriefTest : IDisposable
{
    private static readonly Skill Commit = new("commit", "Commit", "Writes commits the way this team does.", null, "/library/skills/commit");

    private static readonly Skill Migrations = new("laravel-migrations", "Laravel migrations", "Changes the database schema.", null, "/library/skills/laravel-migrations");

    private readonly string home = Directory.CreateTempSubdirectory("crafter-skills-").FullName;

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private readonly PreferencesInMemory saved;

    private readonly SettingsInMemory settings = new();

    private readonly WorkbenchViewModel workbench;

    private readonly LibraryViewModel library;

    private readonly Project crafter;

    private readonly Project lorekeeper;

    public ProjectBriefTest()
    {
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new ClipboardInMemory();
        saved = new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id], Library = "git@github.com:hero/crafter-library.git" });
        var preferences = new PreferenceBook(saved);
        var bookshelf = new Bookshelf(new ShelfInMemory(), settings, time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        library = new LibraryViewModel(preferences, new LibraryInMemory { Contents = new LibraryContents([Commit, Migrations], []) }, time);
        workbench = new WorkbenchViewModel(
            new Project(Directory.CreateDirectory(Path.Combine(home, "home")).FullName, new ProjectName("Home")),
            bookshelf, preferences, picker, new ProjectNamerViewModel(), setup,
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
        Assert.Equal("1 skill", workbench.Status.BriefLabel);
    }

    [Fact]
    public void AGivenSkillReachesOnlyItsProject()
    {
        workbench.Status.ShowBriefCommand.Execute(null);

        Brief.Skills.Single(choice => choice.Skill == Migrations).IsGiven = true;

        Assert.Equal([Migrations], workbench.SkillsFor(crafter));
        Assert.Empty(workbench.SkillsFor(lorekeeper));
        Assert.Equal("1 skill", workbench.Status.BriefLabel);
    }

    [Fact]
    public void HomeGetsTheDefaultsWithNothingToChoose()
    {
        library.MakeDefault(Commit, isDefault: true);

        workbench.Open(workbench.Home);
        workbench.Status.ShowBriefCommand.Execute(null);

        Assert.Equal([Commit], workbench.SkillsFor(workbench.Home));
        Assert.False(workbench.Status.ShowsBrief);
        Assert.True(Overview.IsHome);
        Assert.Null(Overview.Brief);
        Assert.True(Overview.ShowsChats);
    }

    [Fact]
    public void TheStatusBarOpensTheProjectsSkills()
    {
        workbench.NewChatCommand.Execute(null);
        Assert.False(workbench.Desk!.ShowsOverview);

        workbench.Status.ShowBriefCommand.Execute(null);

        Assert.True(workbench.Desk.ShowsOverview);
        Assert.True(Overview.ShowsSkills);
        Assert.Equal(2, Brief.Skills.Count);
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
    public void TheSkillsTabShowsADefaultTickedAndFixed()
    {
        library.MakeDefault(Commit, isDefault: true);

        Overview.ShowSkillsCommand.Execute(null);

        var commit = Brief.Skills.Single(choice => choice.Skill == Commit);
        Assert.True(commit.IsDefault);
        Assert.True(commit.IsGiven);
        Assert.False(commit.CanChange);
        Assert.True(library.Entries.Single(entry => entry.Skill == Commit).IsDefault);

        var migrations = Brief.Skills.Single(choice => choice.Skill == Migrations);
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

    [Fact]
    public void AddsAndRemovesAxiomsOnTheirTab()
    {
        Overview.ShowAxiomsCommand.Execute(null);
        Assert.True(Overview.ShowsAxioms);
        Assert.False(Brief.AddAxiomCommand.CanExecute(null));

        Brief.NewAxiom = "Never add comments";
        Brief.AddAxiomCommand.Execute(null);

        Assert.Equal(["Never add comments"], Brief.Axioms.Select(axiom => axiom.Text));
        Assert.Equal(string.Empty, Brief.NewAxiom);
        Assert.Equal(["Never add comments"], workbench.AxiomsOf(crafter));
        Assert.Empty(workbench.AxiomsOf(lorekeeper));
        Assert.Equal("1 axiom", workbench.Status.BriefLabel);

        Brief.Axioms[0].DropCommand.Execute(null);

        Assert.Empty(Brief.Axioms);
        Assert.Empty(workbench.AxiomsOf(crafter));
        Assert.Equal("No skills or axioms", workbench.Status.BriefLabel);
    }

    [Fact]
    public void TheAxiomsTabShowsAnAxiomHeldFromElsewhere()
    {
        Overview.ShowAxiomsCommand.Execute(null);

        workbench.Hold(crafter, "Always write tests");

        Assert.Equal(["Always write tests"], Brief.Axioms.Select(axiom => axiom.Text));
        Assert.True(Brief.HasAxioms);
    }

    [Fact]
    public async Task EditingAProjectOpensItsDetails()
    {
        workbench.Open(lorekeeper);

        await workbench.Edit(crafter);

        Assert.Equal(crafter, workbench.Current);
        Assert.True(workbench.Desk!.ShowsOverview);
        Assert.True(Overview.ShowsDetails);
        Assert.False(workbench.Namer.IsOpen);
    }

    [Fact]
    public void SavingTheDetailsRenamesTheProjectAndWritesItsSummary()
    {
        Details.Name = "Crafter IDE";
        Details.Summary = " Crafter is my IDE for coding with AI agents. ";
        Details.SaveCommand.Execute(null);

        Assert.Equal("Crafter is my IDE for coding with AI agents.", workbench.SummaryOf(crafter));
        Assert.Null(workbench.SummaryOf(lorekeeper));
        Assert.Equal("Crafter IDE", workbench.Current!.Name.Text);
        Assert.Equal("Crafter IDE", Overview.Name);
        Assert.Contains(workbench.Shelf, item => item.Project.Name.Text == "Crafter IDE");

        Details.Summary = string.Empty;
        Details.SaveCommand.Execute(null);

        Assert.Null(workbench.SummaryOf(crafter));
    }

    [Fact]
    public void DiscardingTheDetailsKeepsTheSummary()
    {
        workbench.Summarize(crafter, "Crafter is my IDE.");

        Details.Summary = "Something else";
        Details.DiscardCommand.Execute(null);

        Assert.Equal("Crafter is my IDE.", Details.Summary);
        Assert.Equal("Crafter is my IDE.", workbench.SummaryOf(crafter));
    }

    [Fact]
    public void AnApprovedSummaryShowsInTheDetailsRightAway()
    {
        workbench.Summarize(crafter, "Crafter is my IDE.");

        Assert.Equal("Crafter is my IDE.", Details.Summary);
        Assert.False(Details.IsChanged);
    }

    [Fact]
    public void StartsTheSummaryFromTheInstructionsFileTheProjectBrings()
    {
        settings.Instructions[lorekeeper.Folder] = new AgentInstructions("CLAUDE.md", "Lorekeeper keeps lore.");

        workbench.Open(lorekeeper);

        Assert.Equal("Start from CLAUDE.md", Details.InheritLabel);
        Details.InheritCommand.Execute(null);
        Details.SaveCommand.Execute(null);
        Assert.Equal("Lorekeeper keeps lore.", workbench.SummaryOf(lorekeeper));
    }

    private ProjectOverviewViewModel Overview => workbench.Desk!.Overview;

    private ProjectBriefViewModel Brief => Overview.Brief!;

    private ProjectDetailsViewModel Details => Overview.Details!;

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => null;
    }
}
