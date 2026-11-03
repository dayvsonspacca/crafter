using Crafter.Agents.ClaudeCode;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Core.Settings;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public static class Composition
{
    private const string App = "crafter";

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Config => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), App);

    private static string Data => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), App);

    public static Workbench Workbench() =>
        new(
            WindowManager.Detect(),
            new Bookshelf(new JsonBookshelfStore(Path.Combine(Config, "projects.json")), new JsonProjectSettingsStore(), () => DateTimeOffset.Now),
            new PreferenceBook(new JsonPreferencesStore(Path.Combine(Config, "preferences.json"), Preferences.Default(Home))),
            new JsonChatArchive(Path.Combine(Data, "chats")),
            Crew());

    private static Crew Crew() => new([new ClaudeCode()]);
}
