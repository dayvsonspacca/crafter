using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Desks;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Library;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Todos;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Desks;

public sealed class ProjectAsideTest
{
    private static readonly Project Crafter = new("/home/hero/crafter", new ProjectName("Crafter"));

    [Fact]
    public void ShowsTheTodosLeftFirstAndTheDoneOnesStruckThrough()
    {
        TestApp.Run(() =>
        {
            var keeper = new Keeper();
            keeper.Held.AddRange([new Todo(1, "Draw the panel", IsDone: true), new Todo(2, "Cite a todo")]);
            var (window, aside) = Show(keeper);

            var texts = Shown(aside);
            Assert.True(texts.IndexOf("Cite a todo") < texts.IndexOf("Draw the panel"));
            Assert.Contains("#2", texts);
            Assert.Contains("1", texts);
            var done = aside.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Draw the panel");
            Assert.Contains("done", done.Classes);
            window.Close();
        });
    }

    [Fact]
    public void CitesATodoFromItsButton()
    {
        TestApp.Run(() =>
        {
            var keeper = new Keeper();
            keeper.Held.Add(new Todo(4, "Cite a todo"));
            var (window, aside) = Show(keeper);

            Click(window, aside.GetVisualDescendants().OfType<Button>().Single(each => Avalonia.Automation.AutomationProperties.GetName(each) == "Cite #4"));

            Assert.Equal([new Todo(4, "Cite a todo")], keeper.Cited);
            window.Close();
        });
    }

    [Fact]
    public void SwitchesToTheAxioms()
    {
        TestApp.Run(() =>
        {
            var (window, aside) = Show(new Keeper(), "Never add comments");

            Click(window, aside.GetVisualDescendants().OfType<Button>().Single(each => each.Content as string == "Axioms"));

            Assert.Contains("Never add comments", Shown(aside));
            Assert.DoesNotContain("Nothing left to do. Add a todo here, or ask the agent to.", Shown(aside));
            window.Close();
        });
    }

    private static (Window Window, ProjectAsideView Aside) Show(Keeper keeper, params string[] axioms)
    {
        var preferences = new PreferenceBook(new PreferencesInMemory(UserPreferences.Default("/home/hero")));
        var library = new LibraryViewModel(preferences, new Library(), TimeProvider.System);
        var brief = new ProjectBriefViewModel(Crafter, library, (_, _) => false, (_, _, _) => { }, _ => axioms, (_, _) => false, (_, _) => false);
        var aside = new ProjectAsideView { DataContext = new ProjectAsideViewModel(new TodoListViewModel(Crafter, keeper), brief) };
        var window = new Window { Width = 320, Height = 600, Content = aside };
        window.Show();
        Settle();

        return (window, aside);
    }

    private static List<string?> Shown(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text)];

    private static void Click(Window window, Control target)
    {
        var at = target.TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Settle();
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Keeper : ITodoKeeper
    {
        public List<Todo> Held { get; } = [];

        public List<Todo> Cited { get; } = [];

        public IReadOnlyList<Todo> TodosOf(Project project) => Held;

        public Todo? Jot(Project project, string text) => null;

        public bool Tick(Project project, int number, bool done) => false;

        public bool Scrap(Project project, int number) => false;

        public void Cite(Project project, Todo todo) => Cited.Add(todo);
    }

    private sealed class Library : ILibrary
    {
        public Task<SyncOutcome> Sync(string repository, CancellationToken stop = default) => Task.FromResult<SyncOutcome>(new Synced("a1b2c3d"));

        public LibraryContents Read() => LibraryContents.Empty;

        public Task<SyncOutcome> Save(SkillDraft skill, string repository, CancellationToken stop = default) => Task.FromResult<SyncOutcome>(new Synced("a1b2c3d"));
    }
}
