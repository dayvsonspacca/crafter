using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Projects;

// What the project in front hands its agent, opened from the status bar: the library skills it gets,
// the defaults among them already ticked, and the axioms it follows in every chat.
public sealed partial class ProjectBriefViewModel(
    LibraryViewModel library,
    Func<Project, Skill, bool> has,
    Action<Project, Skill, bool> give,
    Func<Project, IReadOnlyList<string>> axiomsOf,
    Func<Project, string, bool> hold,
    Func<Project, string, bool> drop) : ObservableObject
{
    private Project? project;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSkills { get; private set; }

    [ObservableProperty]
    public partial bool HasAxioms { get; private set; }

    // The window shows one tab at a time; it opens on the skills.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSkills))]
    public partial bool ShowsAxioms { get; private set; }

    public bool ShowsSkills => !ShowsAxioms;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddAxiomCommand))]
    public partial string NewAxiom { get; set; } = string.Empty;

    public ObservableCollection<SkillChoiceViewModel> Skills { get; } = [];

    public ObservableCollection<AxiomViewModel> Axioms { get; } = [];

    public Project? Project => project;

    public void Open(Project shown)
    {
        project = shown;
        Title = shown.Name.Text;
        Skills.Clear();
        foreach (var skill in library.Skills)
        {
            Skills.Add(new SkillChoiceViewModel(skill, library.IsDefault(skill), has(shown, skill), given => give(shown, skill, given)));
        }

        HasSkills = Skills.Count > 0;
        NewAxiom = string.Empty;
        Recall();
        ShowsAxioms = false;
        IsOpen = true;
    }

    // Shows the axioms again, after they changed from elsewhere, like an agent's proposal.
    public void Recall()
    {
        Axioms.Clear();
        if (project is { } shown)
        {
            foreach (var axiom in axiomsOf(shown))
            {
                Axioms.Add(new AxiomViewModel(axiom, Drop));
            }
        }

        HasAxioms = Axioms.Count > 0;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    private void ShowSkillsTab() => ShowsAxioms = false;

    [RelayCommand]
    private void ShowAxiomsTab() => ShowsAxioms = true;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddAxiom()
    {
        if (project is { } shown && hold(shown, NewAxiom))
        {
            NewAxiom = string.Empty;
            Recall();
        }
    }

    private bool CanAdd() => NewAxiom.Trim().Length > 0;

    private void Drop(AxiomViewModel axiom)
    {
        if (project is { } shown && drop(shown, axiom.Text))
        {
            Recall();
        }
    }
}

public sealed partial class AxiomViewModel(string text, Action<AxiomViewModel> drop)
{
    public string Text { get; } = text;

    [RelayCommand]
    private void Drop() => drop(this);
}

// A default skill reaches every project, so here it shows ticked and cannot be taken away.
public sealed partial class SkillChoiceViewModel : ObservableObject
{
    private readonly Action<bool> give;

    public SkillChoiceViewModel(Skill skill, bool isDefault, bool given, Action<bool> give)
    {
        Skill = skill;
        IsDefault = isDefault;
        IsGiven = isDefault || given;
        this.give = give;
    }

    public Skill Skill { get; }

    public bool IsDefault { get; }

    public bool CanChange => !IsDefault;

    [ObservableProperty]
    public partial bool IsGiven { get; set; }

    partial void OnIsGivenChanged(bool value)
    {
        if (!IsDefault)
        {
            give?.Invoke(value);
        }
    }
}
