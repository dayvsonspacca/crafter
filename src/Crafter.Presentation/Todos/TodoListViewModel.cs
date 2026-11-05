using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Todos;

// A project's todos, the ones left first and the done ones after them.
public sealed partial class TodoListViewModel(Project project, ITodoKeeper keeper) : ObservableObject
{
    public ObservableCollection<TodoViewModel> Todos { get; } = [];

    public Project Project { get; private set; } = project;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewTodo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTodos { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeft))]
    public partial int Left { get; private set; }

    public bool HasLeft => Left > 0;

    public void Describe(Project described) => Project = described;

    // Shows the todos again, after they changed here or from an agent.
    public void Recall()
    {
        var held = keeper.TodosOf(Project);
        Todos.Clear();
        foreach (var todo in held.Where(todo => !todo.IsDone).Concat(held.Where(todo => todo.IsDone)))
        {
            Todos.Add(new TodoViewModel(todo, done => keeper.Tick(Project, todo.Number, done), () => keeper.Scrap(Project, todo.Number), () => keeper.Cite(Project, todo)));
        }

        HasTodos = Todos.Count > 0;
        Left = held.Count(todo => !todo.IsDone);
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (keeper.Jot(Project, NewTodo) is not null)
        {
            NewTodo = string.Empty;
        }
    }

    private bool CanAdd() => NewTodo.Trim().Length > 0;
}
