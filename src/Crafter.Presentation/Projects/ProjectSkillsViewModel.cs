using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Projects;

// The Skills window for the project in front, opened from the status bar: the library skills its
// agent gets, the defaults among them already ticked.
public sealed partial class ProjectSkillsViewModel(
    LibraryViewModel library,
    Func<Project, Skill, bool> has,
    Action<Project, Skill, bool> give) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "Skills";

    [ObservableProperty]
    public partial bool HasSkills { get; private set; }

    public ObservableCollection<SkillChoiceViewModel> Skills { get; } = [];

    public void Open(Project project)
    {
        Title = $"Skills · {project.Name.Text}";
        Skills.Clear();
        foreach (var skill in library.Skills)
        {
            Skills.Add(new SkillChoiceViewModel(skill, library.IsDefault(skill), has(project, skill), given => give(project, skill, given)));
        }

        HasSkills = Skills.Count > 0;
        IsOpen = true;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;
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
