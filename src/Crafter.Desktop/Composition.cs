using Crafter.Agents.ClaudeCode;
using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Platform;
using Crafter.Desktop.Themes;
using Crafter.Desktop.WindowManagers;
using Crafter.Presentation;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
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
        var crew = Crew();
        var preferences = new PreferenceBook(new JsonPreferencesStore(Path.Combine(Config, "preferences.json"), UserPreferences.Default(Home)));
        var bookshelf = new Bookshelf(new JsonBookshelfStore(Path.Combine(Config, "projects.json")), new JsonProjectSettingsStore(), time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);

        var viewModel = new WorkbenchViewModel(
            bookshelf,
            preferences,
            picker,
            new ProjectNamerViewModel(picker),
            setup,
            new PreferencesViewModel(preferences, picker, setup, new AvaloniaThemeSwitcher()),
            new DeskFactory(new JsonChatStore(Path.Combine(Data, "chats")), crew, preferences, clipboard, time),
            clipboard,
            new StatusBarViewModel(new GitBranches(), time));

        return new WorkbenchWindow(WindowManager.Detect(), viewModel);
    }

    private static Crew Crew() => new([new ClaudeCode()]);
}
