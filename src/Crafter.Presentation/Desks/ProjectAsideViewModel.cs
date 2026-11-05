using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Todos;

namespace Crafter.Presentation.Desks;

// The panel on the right of a project: its todos, and the skills and axioms its agent gets.
public sealed partial class ProjectAsideViewModel : ObservableObject
{
    public ProjectAsideViewModel(TodoListViewModel todos, ProjectBriefViewModel brief)
    {
        Todos = todos;
        Brief = brief;
        Todos.Recall();
        Brief.Stock();
        Brief.Recall();
    }

    public TodoListViewModel Todos { get; }

    public ProjectBriefViewModel Brief { get; }

    // The panel opens on the todos.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTodos), nameof(ShowsSkills), nameof(ShowsAxioms))]
    public partial AsideTab Tab { get; private set; }

    public bool ShowsTodos => Tab == AsideTab.Todos;

    public bool ShowsSkills => Tab == AsideTab.Skills;

    public bool ShowsAxioms => Tab == AsideTab.Axioms;

    public void Describe(Project project)
    {
        Todos.Describe(project);
        Brief.Describe(project);
    }

    public void Show(AsideTab tab)
    {
        Tab = tab;
        switch (tab)
        {
            case AsideTab.Todos:
                Todos.Recall();
                break;
            case AsideTab.Skills:
                Brief.Stock();
                break;
            case AsideTab.Axioms:
                Brief.Recall();
                break;
        }
    }

    [RelayCommand]
    private void ShowTodos() => Show(AsideTab.Todos);

    [RelayCommand]
    private void ShowSkills() => Show(AsideTab.Skills);

    [RelayCommand]
    private void ShowAxioms() => Show(AsideTab.Axioms);
}
