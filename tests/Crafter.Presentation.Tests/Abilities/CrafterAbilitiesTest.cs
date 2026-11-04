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
            bookshelf, preferences, picker, new ProjectNamerViewModel(picker), setup,
            new PreferencesViewModel(preferences, picker, setup, new ThemesInMemory(), library),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, time), clipboard,
            new StatusBarViewModel(new Branches(), time));
        abilities = new CrafterAbilities(workbench, library, new Branches());

        crafter = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "crafter")).FullName, new ProjectName("Crafter"));
        lorekeeper = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "lorekeeper")).FullName, new ProjectName("Lorekeeper"));
        workbench.Open(crafter);
        workbench.NewChatCommand.Execute(null);
        workbench.Allow(crafter, Capability.ManageCrafter, allowed: true);
    }

    [Fact]
    public void ListsTheProjectsAndSaysWhichOneAsked()
    {
        var projects = JsonNode.Parse(abilities.ListProjects(crafter.Folder).Text)!.AsArray();

        Assert.Equal(["Crafter", "Lorekeeper"], projects.Select(project => project!["name"]!.GetValue<string>()));
        Assert.Equal("main", projects[0]!["branch"]!.GetValue<string>());
        Assert.Equal(1, projects[0]!["openChats"]!.GetValue<int>());
        Assert.True(projects[0]!["managesCrafter"]!.GetValue<bool>());
        Assert.False(projects[1]!["managesCrafter"]!.GetValue<bool>());
        Assert.True(projects[0]!["isYours"]!.GetValue<bool>());
        Assert.False(projects[1]!["isYours"]!.GetValue<bool>());
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

    [Fact]
    public void CountsTheCapabilitiesOfTheProjectInFront()
    {
        Assert.Equal("1 capability", workbench.Status.CapabilitiesLabel);

        workbench.Open(lorekeeper);
        Assert.Equal("No capabilities", workbench.Status.CapabilitiesLabel);
    }

    [Fact]
    public void OpensTheCapabilitiesOfTheProjectInFrontFromTheStatusBar()
    {
        workbench.Status.ShowCapabilitiesCommand.Execute(null);

        Assert.True(workbench.Capabilities.IsOpen);
        Assert.Equal("Capabilities · Crafter", workbench.Capabilities.Title);
        var choice = Assert.Single(workbench.Capabilities.Choices);
        Assert.Equal("Manages Crafter", choice.Info.Name);
        Assert.True(choice.IsGranted);
        Assert.Equal(["rename_project", "set_project_icon", "save_skill"], choice.Info.Tools.Where(tool => tool.Asks).Select(tool => tool.Name));
    }

    [Fact]
    public void RevokesFromTheCapabilitiesWindow()
    {
        workbench.Status.ShowCapabilitiesCommand.Execute(null);

        workbench.Capabilities.Choices[0].IsGranted = false;

        Assert.False(workbench.Can(crafter, Capability.ManageCrafter));
        Assert.Equal("No capabilities", workbench.Status.CapabilitiesLabel);
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => "main";
    }
}
