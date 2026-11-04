using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Capabilities;

// The Capabilities window for the project in front, opened from the status bar.
public sealed partial class CapabilitiesViewModel(Func<Project, Capability, bool> can, Action<Project, Capability, bool> allow) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "Capabilities";

    public ObservableCollection<CapabilityChoiceViewModel> Choices { get; } = [];

    public void Open(Project project)
    {
        Title = $"Capabilities · {project.Name.Text}";
        Choices.Clear();
        foreach (var info in CapabilityCatalog.All)
        {
            Choices.Add(new CapabilityChoiceViewModel(info, can(project, info.Capability), granted => allow(project, info.Capability, granted)));
        }

        IsOpen = true;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;
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
