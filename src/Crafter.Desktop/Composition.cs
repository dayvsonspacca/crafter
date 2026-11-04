using Crafter.Agents.ClaudeCode;
using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Platform;
using Crafter.Desktop.Themes;
using Crafter.Desktop.WindowManagers;
using Crafter.Presentation;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Settings;
using Crafter.Presentation.Setup;
using Crafter.Storage;

namespace Crafter.Desktop;

public static class Composition
{
    private const string App = "crafter";

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Config => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), App);

    private static string Data => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), App);

    public static Workbench Workbench()
    {
        var time = TimeProvider.System;
        var clipboard = new DesktopClipboard();
        var crew = Crew();
        var preferences = new PreferenceBook(new JsonPreferencesStore(Path.Combine(Config, "preferences.json"), UserPreferences.Default(Home)));
        var bookshelf = new Bookshelf(new JsonBookshelfStore(Path.Combine(Config, "projects.json")), new JsonProjectSettingsStore(), time.GetLocalNow);
        var picker = new FolderPicker(preferences, bookshelf);
        var setup = new SetupScreen(crew, preferences, clipboard);

        var screen = new WorkbenchScreen(
            bookshelf,
            preferences,
            picker,
            new ProjectNamer(picker),
            setup,
            new PreferencesScreen(preferences, picker, setup, new ApplicationThemeSwitcher()),
            new DeskFactory(new JsonChatArchive(Path.Combine(Data, "chats")), crew, preferences, clipboard, time),
            clipboard);

        return new Workbench(WindowManager.Detect(), screen);
    }

    private static Crew Crew() => new([new ClaudeCode()]);
}
