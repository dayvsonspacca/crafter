using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Capabilities;

// The Capabilities window for the project in front, opened from the status bar: the powers its
// agent holds and the library skills it gets, the defaults among them already ticked.
public sealed partial class CapabilitiesViewModel(
    Func<Project, Capability, bool> can,
    Action<Project, Capability, bool> allow,
    LibraryViewModel library,
    Func<Project, Skill, bool> has,
    Action<Project, Skill, bool> give) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "Capabilities";

    [ObservableProperty]
    public partial bool HasSkills { get; private set; }

    // The window shows one tab at a time; it opens on the capabilities.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCapabilities))]
    public partial bool ShowsSkills { get; private set; }

    public bool ShowsCapabilities => !ShowsSkills;

    public ObservableCollection<CapabilityChoiceViewModel> Choices { get; } = [];

    public ObservableCollection<SkillChoiceViewModel> Skills { get; } = [];

    public void Open(Project project)
    {
        Title = $"Capabilities · {project.Name.Text}";
        Choices.Clear();
        foreach (var info in CapabilityCatalog.All)
        {
            Choices.Add(new CapabilityChoiceViewModel(info, can(project, info.Capability), granted => allow(project, info.Capability, granted)));
        }

        Skills.Clear();
        foreach (var skill in library.Skills)
        {
            Skills.Add(new SkillChoiceViewModel(skill, library.IsDefault(skill), has(project, skill), given => give(project, skill, given)));
        }

        HasSkills = Skills.Count > 0;
        ShowsSkills = false;
        IsOpen = true;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    private void ShowCapabilitiesTab() => ShowsSkills = false;

    [RelayCommand]
    private void ShowSkillsTab() => ShowsSkills = true;
}

public sealed partial class CapabilityChoiceViewModel : ObservableObject
{
    private readonly Action<bool> allow;

    public CapabilityChoiceViewModel(CapabilityInfo info, bool granted, Action<bool> allow)
    {
        Info = info;
        IsGranted = granted;
        this.allow = allow;
    }

    public CapabilityInfo Info { get; }

    [ObservableProperty]
    public partial bool IsGranted { get; set; }

    partial void OnIsGrantedChanged(bool value) => allow?.Invoke(value);
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
