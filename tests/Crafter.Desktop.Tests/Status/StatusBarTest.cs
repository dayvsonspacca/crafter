using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Core.Agents.Background;
using Crafter.Core.Conversations;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.Status;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Status;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Status;

public sealed class StatusBarTest
{
    [Fact]
    public void ShowsTheBranchAndHowManyJobsRun()
    {
        TestApp.Run(() =>
        {
            var (window, bar, session) = Show();

            session.Run(new Job("b-1", JobKind.Shell, "Dev server"), new Job("b-2", JobKind.Monitor, "Watch the log"));
            Settle();

            var texts = bar.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToList();
            Assert.Contains("feature/status-bar", texts);
            Assert.Contains("2 jobs", texts);
            window.Close();
        });
    }

    [Fact]
    public void OpensTheJobsWindowFromTheBarAndClosesItWithEscape()
    {
        TestApp.Run(() =>
        {
            var (window, bar, session) = Show();
            session.Run(new Job("b-1", JobKind.Shell, "Dev server"));
            Settle();
            var jobs = window.GetVisualDescendants().OfType<JobsView>().Single();
            Assert.False(jobs.IsVisible);

            var button = bar.GetVisualDescendants().OfType<Button>().Single();
            window.MouseDown(button.TranslatePoint(new Point(8, 8), window)!.Value, MouseButton.Left);
            window.MouseUp(button.TranslatePoint(new Point(8, 8), window)!.Value, MouseButton.Left);
            Settle();

            Assert.True(jobs.IsVisible);
            Assert.Contains("Dev server", jobs.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            Assert.False(jobs.IsVisible);
            window.Close();
        });
    }

    private static (Window Window, StatusBarView Bar, ScriptedSession Session) Show()
    {
        var session = new ScriptedSession();
        var agent = new ScriptedAgent { Sessions = () => session };
        var chat = new ConversationViewModel(
            new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session)),
            agent,
            new PreferenceBook(new PreferencesInMemory(UserPreferences.Default("/home/hero"))),
            "crafter",
            new Keeper(),
            new Clipboard(),
            new Files(),
            TimeProvider.System);
        var status = new StatusBarViewModel(new Branches(), TimeProvider.System) { Chat = chat };
        status.Watch(new Project("/home/hero/crafter"));

        var bar = new StatusBarView { DataContext = status };
        var jobs = new JobsView { DataContext = chat };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(new Panel { Children = { new Border(), jobs } });
        var window = new Window { Width = 900, Height = 500, Content = root };
        window.Show();
        Settle();

        return (window, bar, session);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => "feature/status-bar";
    }

    private sealed class Keeper : IChatKeeper
    {
        public void Select(ConversationViewModel chat)
        {
        }

        public void Close(ConversationViewModel chat)
        {
        }

        public void Keep(ConversationViewModel chat)
        {
        }
    }

    private sealed class Clipboard : IClipboard
    {
        public Task Copy(string text) => Task.CompletedTask;
    }

    private sealed class Files : IProjectFiles
    {
        public string Folder => "/home/hero/crafter";

        public Task<IReadOnlyList<string>> List(CancellationToken stop = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
