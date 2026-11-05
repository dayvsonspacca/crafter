using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Presentation.Todos;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Todos;

public sealed class TodoListTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-todos-").FullName;

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private readonly WorkbenchViewModel workbench;

    private readonly Project crafter;

    public TodoListTest()
    {
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new ClipboardInMemory();
        var preferences = new PreferenceBook(new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id] }));
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), time.GetLocalNow);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        var library = new LibraryViewModel(preferences, new LibraryInMemory(), time);
        workbench = new WorkbenchViewModel(
            new Project(Directory.CreateDirectory(Path.Combine(home, "home")).FullName, new ProjectName("Home")),
            bookshelf, preferences, picker, new ProjectNamerViewModel(), setup,
            new PreferencesViewModel(preferences, picker, setup, new ThemesInMemory(), library),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, time), clipboard,
            new StatusBarViewModel(new Branches(), time));

        crafter = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "crafter")).FullName, new ProjectName("Crafter"));
        workbench.Open(crafter);
    }

    [Fact]
    public void ThePanelOpensOnTheTodos()
    {
        Assert.True(workbench.ShowsAside);
        Assert.True(workbench.Desk!.Aside!.ShowsTodos);
        Assert.False(List.HasTodos);
    }

    [Fact]
    public void AddsATodoAndCountsTheOnesLeft()
    {
        Assert.False(List.AddCommand.CanExecute(null));

        List.NewTodo = "Draw the panel";
        List.AddCommand.Execute(null);
        List.NewTodo = "Cite a todo";
        List.AddCommand.Execute(null);

        Assert.Equal(string.Empty, List.NewTodo);
        Assert.Equal(["#1", "#2"], List.Todos.Select(todo => todo.Label));
        Assert.Equal(2, List.Left);
        Assert.True(List.HasLeft);
    }

    [Fact]
    public void ADoneTodoMovesBelowTheOnesLeft()
    {
        workbench.Jot(crafter, "Draw the panel");
        workbench.Jot(crafter, "Cite a todo");

        List.Todos[0].IsDone = true;

        Assert.Equal([2, 1], List.Todos.Select(todo => todo.Number));
        Assert.True(List.Todos[1].IsDone);
        Assert.Equal(1, List.Left);
        Assert.True(workbench.TodosOf(crafter)[0].IsDone);
    }

    [Fact]
    public void RemovesATodo()
    {
        workbench.Jot(crafter, "Draw the panel");

        List.Todos[0].RemoveCommand.Execute(null);

        Assert.Empty(List.Todos);
        Assert.Empty(workbench.TodosOf(crafter));
    }

    [Fact]
    public void ATodoFromAnAgentShowsRightAway()
    {
        workbench.Jot(crafter, "Draw the panel");

        Assert.Equal(["Draw the panel"], List.Todos.Select(todo => todo.Text));
    }

    [Fact]
    public void CitingATodoPutsItInTheComposerOfTheChatInFront()
    {
        workbench.NewChatCommand.Execute(null);
        var chat = workbench.Desk!.Selected!;
        workbench.Jot(crafter, "Draw the panel");

        List.Todos[0].CiteCommand.Execute(null);
        List.Todos[0].CiteCommand.Execute(null);

        var cited = Assert.Single(chat.Attached);
        Assert.True(cited.IsTodo);
        Assert.Equal("#1", cited.Label);
        Assert.Equal("Draw the panel", cited.Name);
    }

    [Fact]
    public void CitingATodoWithNoChatInFrontStartsOne()
    {
        workbench.Jot(crafter, "Draw the panel");

        List.Todos[0].CiteCommand.Execute(null);

        var chat = workbench.Desk!.Selected!;
        Assert.Equal(new Todo(1, "Draw the panel"), Assert.Single(chat.Attached).Todo);
    }

    [Fact]
    public void ACitedTodoIsSentAsItsLineAndShownOnTheMessage()
    {
        workbench.NewChatCommand.Execute(null);
        var chat = workbench.Desk!.Selected!;
        workbench.Jot(crafter, "Draw the panel");
        List.Todos[0].CiteCommand.Execute(null);

        chat.Draft = "Do this one";
        chat.CraftCommand.Execute(null);

        var asked = chat.Messages.OfType<YourMessageViewModel>().Single();
        Assert.Equal("Do this one\n\nTodo #1: Draw the panel", asked.Prompt.Text);
        Assert.Equal([new Todo(1, "Draw the panel")], asked.Todos);
        Assert.Empty(chat.Attached);
    }

    private TodoListViewModel List => workbench.Desk!.Aside!.Todos;

    public void Dispose() => Directory.Delete(home, recursive: true);

    private sealed class Branches : IBranches
    {
        public string? Of(Project project) => null;
    }
}
