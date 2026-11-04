using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Agents;

public sealed partial class SetupViewModel : ObservableObject
{
    private readonly Crew crew;

    private readonly PreferenceBook preferences;

    public SetupViewModel(Crew crew, PreferenceBook preferences, IClipboard clipboard)
    {
        this.crew = crew;
        this.preferences = preferences;
        crew.Hire(preferences.Current.Agents);
        Panels = [.. crew.Roster.Select(agent => new AgentPanelViewModel(agent, preferences, clipboard) { IsMarked = crew.IsHired(agent.Id) })];
        IsPicking = crew.IsHiring;

        foreach (var panel in Panels)
        {
            panel.PropertyChanged += Follow;
        }

        Lock();
    }

    public IReadOnlyList<AgentPanelViewModel> Panels { get; }

    public bool NeedsSetup => IsPicking || IsConfirming || Waiting > 0;

    public string Headline => this switch
    {
        { IsPicking: true } => "Pick your agents",
        { IsConfirming: true } => "Checking your agents…",
        { Waiting: 1 } => "One agent needs attention",
        _ => $"{Waiting} agents need attention",
    };

    public string Explanation => this switch
    {
        { IsPicking: true } => "Crafter checks only the agents you mark.",
        { IsConfirming: true } => "Crafter is checking the agents you marked.",
        { Waiting: 1 } => "Fix it, then check again.",
        _ => "Fix them, then check again.",
    };

    private int Waiting => Panels.Count(panel => panel.IsMarked && panel.NeedsAttention);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsSetup), nameof(Headline), nameof(Explanation))]
    public partial bool IsPicking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsSetup), nameof(Headline), nameof(Explanation))]
    public partial bool IsConfirming { get; set; }

    [RelayCommand]
    public Task Check() => Task.WhenAll(Panels.Where(panel => panel.IsMarked).Select(panel => panel.CheckCommand.ExecuteAsync(null)));

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task Continue()
    {
        Hire();
        IsConfirming = true;
        IsPicking = false;
        await Check();
        IsConfirming = false;
    }

    private bool CanContinue() => Panels.Any(panel => panel.IsMarked);

    [RelayCommand]
    private void ChangeAgents() => IsPicking = true;

    partial void OnIsPickingChanged(bool value) => Lock();

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(AgentPanelViewModel.IsMarked) && sender is AgentPanelViewModel panel)
        {
            ContinueCommand.NotifyCanExecuteChanged();
            Lock();

            if (!IsPicking)
            {
                Hire();
                if (panel.IsMarked)
                {
                    _ = panel.CheckCommand.ExecuteAsync(null);
                }
            }
        }

        if (changed.PropertyName is nameof(AgentPanelViewModel.IsMarked) or nameof(AgentPanelViewModel.Readiness))
        {
            NotifyDerived();
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(Explanation));
    }

    private void Lock()
    {
        var marked = Panels.Count(panel => panel.IsMarked);

        foreach (var panel in Panels)
        {
            panel.IsLocked = !IsPicking && panel.IsMarked && marked == 1;
        }
    }

    private void Hire()
    {
        List<AgentId> marked = [.. Panels.Where(panel => panel.IsMarked).Select(panel => panel.Id)];

        crew.Hire(marked);
        preferences.Hire(marked);
    }
}
