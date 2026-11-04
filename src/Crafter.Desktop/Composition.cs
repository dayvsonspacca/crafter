using Avalonia.Threading;
using Crafter.Agents.ClaudeCode;
using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Platform;
using Crafter.Desktop.Themes;
using Crafter.Desktop.WindowManagers;
using Crafter.Desktop.Mcp;
using Crafter.Presentation;
using Crafter.Presentation.Abilities;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Storage;

namespace Crafter.Desktop;

public static class Composition
{
    private const string App = "crafter";

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Config => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), App);

    private static string Data => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), App);

    public static WorkbenchWindow MainWindow()
    {
        var time = TimeProvider.System;
        var clipboard = new AvaloniaClipboard();

        // Sessions ask for their tool servers, skills and axioms when they start, after both below are up.
        CrafterMcp? mcp = null;
        WorkbenchViewModel? workbench = null;
        var crew = new Crew([new ClaudeCode(
            servers: project => mcp?.ServersFor(project) ?? [],
            skills: project => workbench is { } shown ? OnUi(() => shown.SkillsFor(project)) : [],
            shelves: Path.Combine(Data, "claude-code", "skills"),
            axioms: project => workbench is { } shown ? OnUi(() => shown.AxiomsOf(project)) : [])]);
        var preferences = new PreferenceBook(new JsonPreferencesStore(Path.Combine(Config, "preferences.json"), UserPreferences.Default(Home)));
        var bookshelf = new Bookshelf(new JsonBookshelfStore(Path.Combine(Config, "projects.json")), new JsonProjectSettingsStore(), time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        var branches = new GitBranches();
        var library = new LibraryViewModel(preferences, new GitLibrary(Path.Combine(Data, "library")), time);

        var home = new Project(Directory.CreateDirectory(Path.Combine(Data, "home")).FullName, new ProjectName("Home"));
        var viewModel = new WorkbenchViewModel(
            home,
            bookshelf,
            preferences,
            picker,
            new ProjectNamerViewModel(picker),
            setup,
            new PreferencesViewModel(preferences, picker, setup, new AvaloniaThemeSwitcher(), library),
            new DeskFactory(new JsonChatStore(Path.Combine(Data, "chats")), crew, preferences, clipboard, time),
            clipboard,
            new StatusBarViewModel(branches, time));

        workbench = viewModel;
        mcp = new CrafterMcp(viewModel, new CrafterAbilities(viewModel, library, branches));
        mcp.Start();

        return new WorkbenchWindow(WindowManager.Detect(), viewModel);
    }

    private static T OnUi<T>(Func<T> read) => Dispatcher.UIThread.CheckAccess() ? read() : Dispatcher.UIThread.Invoke(read);
}
