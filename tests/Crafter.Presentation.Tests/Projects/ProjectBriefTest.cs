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
        Assert.Equal("1 skill", workbench.Status.BriefLabel);
    }

    [Fact]
    public void AGivenSkillReachesOnlyItsProject()
    {
        workbench.Status.ShowBriefCommand.Execute(null);

        workbench.Brief.Skills.Single(choice => choice.Skill == Migrations).IsGiven = true;

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
        Assert.False(workbench.Brief.IsOpen);
    }

    [Fact]
    public void AProjectHasItsSkillsToChoose()
    {
        workbench.Status.ShowBriefCommand.Execute(null);

        Assert.True(workbench.Status.ShowsBrief);
        Assert.True(workbench.Brief.IsOpen);
        Assert.Equal("Crafter", workbench.Brief.Title);
        Assert.True(workbench.Brief.ShowsSkills);
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
    public void TheSkillsWindowShowsADefaultTickedAndFixed()
    {
        library.MakeDefault(Commit, isDefault: true);

        workbench.Status.ShowBriefCommand.Execute(null);

        var commit = workbench.Brief.Skills.Single(choice => choice.Skill == Commit);
        Assert.True(commit.IsDefault);
        Assert.True(commit.IsGiven);
        Assert.False(commit.CanChange);
        Assert.True(library.Entries.Single(entry => entry.Skill == Commit).IsDefault);

        var migrations = workbench.Brief.Skills.Single(choice => choice.Skill == Migrations);
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
    public void AddsAndRemovesAxiomsFromTheWindow()
    {
        workbench.Status.ShowBriefCommand.Execute(null);
        workbench.Brief.ShowAxiomsTabCommand.Execute(null);
        Assert.True(workbench.Brief.ShowsAxioms);
        Assert.False(workbench.Brief.AddAxiomCommand.CanExecute(null));

        workbench.Brief.NewAxiom = "Never add comments";
        workbench.Brief.AddAxiomCommand.Execute(null);

        Assert.Equal(["Never add comments"], workbench.Brief.Axioms.Select(axiom => axiom.Text));
        Assert.Equal(string.Empty, workbench.Brief.NewAxiom);
        Assert.Equal(["Never add comments"], workbench.AxiomsOf(crafter));
        Assert.Empty(workbench.AxiomsOf(lorekeeper));
        Assert.Equal("1 axiom", workbench.Status.BriefLabel);

        workbench.Brief.Axioms[0].DropCommand.Execute(null);

        Assert.Empty(workbench.Brief.Axioms);
        Assert.Empty(workbench.AxiomsOf(crafter));
        Assert.Equal("No skills or axioms", workbench.Status.BriefLabel);
    }

    [Fact]
    public void TheOpenWindowShowsAnAxiomHeldFromElsewhere()
    {
        workbench.Status.ShowBriefCommand.Execute(null);

        workbench.Hold(crafter, "Always write tests");

        Assert.Equal(["Always write tests"], workbench.Brief.Axioms.Select(axiom => axiom.Text));
        Assert.True(workbench.Brief.HasAxioms);
    }

    [Fact]
    public async Task EditingAProjectWritesItsSummary()
    {
        var editing = workbench.Edit(crafter);
        Assert.True(workbench.Namer.IsOpen);
        Assert.Equal(string.Empty, workbench.Namer.Summary);

        workbench.Namer.Summary = " Crafter is my IDE for coding with AI agents. ";
        workbench.Namer.Finish();
        await editing;

        Assert.Equal("Crafter is my IDE for coding with AI agents.", workbench.SummaryOf(crafter));
        Assert.Null(workbench.SummaryOf(lorekeeper));

        editing = workbench.Edit(crafter);
        Assert.Equal("Crafter is my IDE for coding with AI agents.", workbench.Namer.Summary);
        workbench.Namer.Summary = string.Empty;
        workbench.Namer.Finish();
        await editing;

        Assert.Null(workbench.SummaryOf(crafter));
    }

    [Fact]
    public async Task CancellingTheEditKeepsTheSummary()
    {
        workbench.Summarize(crafter, "Crafter is my IDE.");

        var editing = workbench.Edit(crafter);
        workbench.Namer.Summary = "Something else";
        workbench.Namer.Back();
        await editing;

        Assert.Equal("Crafter is my IDE.", workbench.SummaryOf(crafter));
    }

    [Fact]
    public async Task StartsTheSummaryFromTheInstructionsFileTheProjectBrings()
    {
        settings.Instructions[crafter.Folder] = new AgentInstructions("CLAUDE.md", "Crafter is my IDE.");

        var editing = workbench.Edit(crafter);

        Assert.True(workbench.Namer.CanInherit);
        Assert.Equal("Start from CLAUDE.md", workbench.Namer.InheritLabel);

        workbench.Namer.InheritCommand.Execute(null);

        Assert.Equal("Crafter is my IDE.", workbench.Namer.Summary);
        Assert.False(workbench.Namer.CanInherit);
        Assert.Null(workbench.SummaryOf(crafter));

        workbench.Namer.Finish();
        await editing;
        Assert.Equal("Crafter is my IDE.", workbench.SummaryOf(crafter));
    }

    [Fact]
    public void OffersNoInstructionsFileToAProjectWithoutOne()
    {
        _ = workbench.Edit(crafter);

        Assert.False(workbench.Namer.CanInherit);
        Assert.False(workbench.Namer.InheritCommand.CanExecute(null));
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => null;
    }
}
