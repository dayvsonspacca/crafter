using System.Text.Json.Nodes;
using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Abilities;
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

namespace Crafter.Presentation.Tests.Abilities;

public sealed class CrafterAbilitiesTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-abilities-").FullName;

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private readonly LibraryInMemory store = new();

    private readonly SettingsInMemory settings = new();

    private readonly WorkbenchViewModel workbench;

    private readonly LibraryViewModel library;

    private readonly CrafterAbilities abilities;

    private readonly Project crafter;

    private readonly Project lorekeeper;

    public CrafterAbilitiesTest()
    {
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new ClipboardInMemory();
        var preferences = new PreferenceBook(new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id], Library = "git@github.com:hero/crafter-library.git" }));
        var bookshelf = new Bookshelf(new ShelfInMemory(), settings, time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        library = new LibraryViewModel(preferences, store, time);
        workbench = new WorkbenchViewModel(
            new Project(Directory.CreateDirectory(Path.Combine(home, "home")).FullName, new ProjectName("Home")),
            bookshelf, preferences, picker, new ProjectNamerViewModel(picker), setup,
            new PreferencesViewModel(preferences, picker, setup, new ThemesInMemory(), library),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, time), clipboard,
            new StatusBarViewModel(new Branches(), time));
        abilities = new CrafterAbilities(workbench, library, new Branches());

        crafter = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "crafter")).FullName, new ProjectName("Crafter"));
        lorekeeper = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "lorekeeper")).FullName, new ProjectName("Lorekeeper"));
        workbench.Open(crafter);
        workbench.NewChatCommand.Execute(null);
    }

    [Fact]
    public void ListsTheProjectsButNotHome()
    {
        var projects = JsonNode.Parse(abilities.ListProjects().Text)!.AsArray();

        Assert.Equal(["Crafter", "Lorekeeper"], projects.Select(project => project!["name"]!.GetValue<string>()));
        Assert.Equal(crafter.Folder, projects[0]!["folder"]!.GetValue<string>());
        Assert.Equal("main", projects[0]!["branch"]!.GetValue<string>());
        Assert.Equal(1, projects[0]!["openChats"]!.GetValue<int>());
    }

    [Fact]
    public void AddsAFolderWithoutLeavingTheProjectInFront()
    {
        var beacon = Directory.CreateDirectory(Path.Combine(home, "beacon")).FullName;

        var told = abilities.AddProject("beacon", null);

        Assert.False(told.IsError);
        Assert.Equal($"Added beacon to Crafter, from {beacon}.", told.Text);
        Assert.Contains(workbench.Shelf, item => item.Project.Folder == beacon);
        Assert.Equal(crafter, workbench.Current);
    }

    [Fact]
    public void AddsAFolderUnderTheNameGiven()
    {
        var reborn = Directory.CreateDirectory(Path.Combine(home, "reborn")).FullName;

        Assert.False(abilities.AddProject(reborn, "Reborn · web").IsError);
        Assert.Contains(workbench.Shelf, item => item.Project.Folder == reborn && item.Name == "Reborn · web");
    }

    [Fact]
    public void RefusesToAddAMissingFolderHomeOrAProjectTwice()
    {
        Assert.True(abilities.AddProject(" ", null).IsError);
        Assert.True(abilities.AddProject("missing", null).IsError);
        Assert.True(abilities.AddProject(workbench.Home.Folder, null).IsError);
        Assert.Equal($"Crafter is already in Crafter, from {crafter.Folder}.", abilities.AddProject(crafter.Folder, null).Text);
        Assert.Equal(2, workbench.Projects.Count);
    }

    [Fact]
    public void CreatesAnEmptyFolderUnderTheProjectsRoot()
    {
        var told = abilities.CreateProject("Lorekeeper Web", null);

        var folder = Path.Combine(home, "lorekeeper-web");
        Assert.False(told.IsError);
        Assert.Equal($"Created {folder} and added Lorekeeper Web to Crafter.", told.Text);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
        Assert.Contains(workbench.Shelf, item => item.Project.Folder == folder && item.Name == "Lorekeeper Web");
        Assert.Equal(crafter, workbench.Current);
    }

    [Fact]
    public void CreatesAProjectWhereItIsTold()
    {
        Assert.False(abilities.CreateProject("Beacon", "work/beacon").IsError);
        Assert.Contains(workbench.Projects, project => project.Folder == Path.Combine(home, "work", "beacon"));

        var empty = Directory.CreateDirectory(Path.Combine(home, "empty")).FullName;
        Assert.False(abilities.CreateProject("Empty", empty).IsError);
    }

    [Fact]
    public void RefusesToCreateOverFilesOrWithoutAName()
    {
        var taken = Directory.CreateDirectory(Path.Combine(home, "taken")).FullName;
        File.WriteAllText(Path.Combine(taken, "README.md"), "mine");

        Assert.True(abilities.CreateProject("Taken", null).IsError);
        Assert.True(abilities.CreateProject(" ", null).IsError);
        Assert.Equal(2, workbench.Projects.Count);
    }

    [Fact]
    public void OpensAProject()
    {
        Assert.Equal("Opened Lorekeeper.", abilities.OpenProject(lorekeeper.Folder).Text);
        Assert.Equal(lorekeeper, workbench.Current);
        Assert.True(abilities.OpenProject("/nowhere").IsError);
    }

    [Fact]
    public void RemovesAProjectAndLeavesItsFolder()
    {
        var told = abilities.RemoveProject(lorekeeper.Folder);

        Assert.False(told.IsError);
        Assert.DoesNotContain(lorekeeper, workbench.Projects);
        Assert.True(Directory.Exists(lorekeeper.Folder));
        Assert.True(abilities.RemoveProject(lorekeeper.Folder).IsError);
    }

    [Fact]
    public async Task KeepsAnAxiomOnlyOnceTheUserApprovesIt()
    {
        List<string> asked = [];
        Task<bool> Approve(Project project, string axiom)
        {
            asked.Add($"{project.Name.Text}: {axiom}");

            return Task.FromResult(true);
        }

        var told = await abilities.ProposeAxiom(workbench.Home.Folder, lorekeeper.Folder, " Always write tests ", Approve);

        Assert.Equal("Lorekeeper now holds the axiom \"Always write tests\"; its chats follow it from their next session.", told.Text);
        Assert.Equal(["Lorekeeper: Always write tests"], asked);
        Assert.Equal(["Always write tests"], workbench.AxiomsOf(lorekeeper));

        Assert.Equal("Lorekeeper already holds that axiom.", (await abilities.ProposeAxiom(workbench.Home.Folder, lorekeeper.Folder, "Always write tests", Approve)).Text);
        Assert.Single(asked);
        var listed = JsonNode.Parse(abilities.ListProjects().Text)!.AsArray();
        Assert.Equal(["Always write tests"], listed[1]!["axioms"]!.AsArray().Select(axiom => axiom!.GetValue<string>()));
    }

    [Fact]
    public async Task KeepsNothingTheUserDeclines()
    {
        var told = await abilities.ProposeAxiom(crafter.Folder, null, "Never add comments", (_, _) => Task.FromResult(false));

        Assert.False(told.IsError);
        Assert.Equal("The user declined the axiom \"Never add comments\"; Crafter does not hold it.", told.Text);
        Assert.Empty(workbench.AxiomsOf(crafter));
    }

    [Fact]
    public async Task AProjectsAgentProposesAxiomsForItsOwnProjectOnly()
    {
        static Task<bool> Yes(Project project, string axiom) => Task.FromResult(true);

        Assert.False((await abilities.ProposeAxiom(crafter.Folder, null, "Never add comments", Yes)).IsError);
        Assert.False((await abilities.ProposeAxiom(crafter.Folder, crafter.Folder, "Always write tests", Yes)).IsError);
        Assert.Equal(["Never add comments", "Always write tests"], workbench.AxiomsOf(crafter));

        Assert.True((await abilities.ProposeAxiom(crafter.Folder, lorekeeper.Folder, "Never add comments", Yes)).IsError);
        Assert.True((await abilities.ProposeAxiom(crafter.Folder, null, " ", Yes)).IsError);
        Assert.True((await abilities.ProposeAxiom(workbench.Home.Folder, "/nowhere", "Never add comments", Yes)).IsError);
        Assert.Empty(workbench.AxiomsOf(lorekeeper));
    }

    [Fact]
    public void RemovesAnAxiom()
    {
        workbench.Hold(lorekeeper, "Always write tests");

        Assert.False(abilities.RemoveAxiom(lorekeeper.Folder, "Always write tests").IsError);
        Assert.Empty(workbench.AxiomsOf(lorekeeper));
        Assert.True(abilities.RemoveAxiom(lorekeeper.Folder, "Always write tests").IsError);
    }

    [Fact]
    public void RenamesAProjectWhereverItShows()
    {
        var told = abilities.RenameProject(lorekeeper.Folder, "Lorekeeper · desktop");

        Assert.False(told.IsError);
        Assert.Equal("Renamed Lorekeeper to Lorekeeper · desktop.", told.Text);
        Assert.Contains(workbench.Shelf, item => item.Project.Name.Text == "Lorekeeper · desktop");
    }

    [Fact]
    public void RefusesToRenameWhatItCannotFindOrToANameOfNothing()
    {
        Assert.True(abilities.RenameProject("/nowhere", "x").IsError);
        Assert.True(abilities.RenameProject(crafter.Folder, " ").IsError);
    }

    [Fact]
    public void SetsAProjectIconAndTheSidebarShowsIt()
    {
        var image = Path.Combine(home, "hyprland.png");
        File.WriteAllBytes(image, [0x89, 0x50, 0x4E, 0x47]);

        var told = abilities.SetProjectIcon(crafter.Folder, lorekeeper.Folder, image);

        Assert.False(told.IsError);
        Assert.Equal("Lorekeeper now shows hyprland.png as its icon, kept in .crafter/icon.png.", told.Text);
        Assert.Equal([image], settings.Adopted);
        Assert.Contains(workbench.Shelf, item => item.Project == lorekeeper && item.Project.Icon == ".crafter/icon.png");
    }

    [Fact]
    public void ReadsARelativeIconFromTheAskingProject()
    {
        File.WriteAllBytes(Path.Combine(crafter.Folder, "logo.webp"), [0x52, 0x49, 0x46, 0x46]);

        Assert.False(abilities.SetProjectIcon(crafter.Folder, lorekeeper.Folder, "logo.webp").IsError);
        Assert.Equal([Path.Combine(crafter.Folder, "logo.webp")], settings.Adopted);
    }

    [Fact]
    public void RefusesAnIconItCannotShow()
    {
        var text = Path.Combine(home, "notes.txt");
        File.WriteAllText(text, "not an image");

        Assert.True(abilities.SetProjectIcon(crafter.Folder, "/nowhere", text).IsError);
        Assert.True(abilities.SetProjectIcon(crafter.Folder, lorekeeper.Folder, " ").IsError);
        Assert.True(abilities.SetProjectIcon(crafter.Folder, lorekeeper.Folder, Path.Combine(home, "missing.png")).IsError);
        Assert.True(abilities.SetProjectIcon(crafter.Folder, lorekeeper.Folder, text).IsError);
        Assert.Empty(settings.Adopted);
    }

    [Fact]
    public void ReadsASkillWithItsInstructions()
    {
        var folder = Directory.CreateDirectory(Path.Combine(home, "library", "skills", "pr-review")).FullName;
        File.WriteAllText(Path.Combine(folder, "instructions.md"), "Read the diff.");
        store.Contents = new LibraryContents([new Skill("pr-review", "PR review", "Reviews a pull request.", null, folder)], []);
        library.SyncCommand.Execute(null);

        var skill = JsonNode.Parse(abilities.ReadSkill("pr-review").Text)!;

        Assert.Equal("Read the diff.", skill["instructions"]!.GetValue<string>());
        Assert.True(abilities.ReadSkill("missing").IsError);
    }

    [Fact]
    public async Task SavesASkillThroughTheLibraryAndSaysWhyItCouldNot()
    {
        var draft = new SkillDraft("pr-review", "PR review", "Reviews a pull request.", null, "Read the diff.");

        Assert.Equal("Saved pr-review to the library and pushed it (a1b2c3d).", (await abilities.SaveSkill(draft)).Text);
        Assert.Equal([draft], store.Saved);

        store.Outcome = new SyncFailed("rejected: fetch first");
        var failed = await abilities.SaveSkill(draft);
        Assert.True(failed.IsError);
        Assert.Equal("Could not save pr-review: rejected: fetch first", failed.Text);
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => "main";
    }
}
