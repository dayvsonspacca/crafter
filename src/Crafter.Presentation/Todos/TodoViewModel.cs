using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Todos;

public sealed partial class TodoViewModel : ObservableObject
{
    private readonly Func<bool, bool>? tick;

    private readonly Func<bool> scrap;

    private readonly Action cite;

    public TodoViewModel(Todo todo, Func<bool, bool> tick, Func<bool> scrap, Action cite)
    {
        Todo = todo;
        IsDone = todo.IsDone;
        this.tick = tick;
        this.scrap = scrap;
        this.cite = cite;
    }

    public Todo Todo { get; }

    public int Number => Todo.Number;

    public string Text => Todo.Text;

    public string Label => $"#{Todo.Number}";

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    partial void OnIsDoneChanged(bool value) => tick?.Invoke(value);

    [RelayCommand]
    private void Remove() => scrap();

    [RelayCommand]
    private void Cite() => cite();
}
