using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Settings;
using Crafter.Desktop.Agents;
using Crafter.Desktop.Platform;

namespace Crafter.Desktop.Setup;

public sealed partial class SetupScreen : ObservableObject
{
    private readonly Crew crew;

    private readonly PreferenceBook preferences;

    public SetupScreen(Crew crew, PreferenceBook preferences, IClipboard clipboard)
    {
        this.crew = crew;
        this.preferences = preferences;
        crew.Hire(preferences.Current.Agents);
        Panels = [.. crew.Roster.Select(agent => new AgentPanel(agent, preferences, clipboard) { IsMarked = crew.IsHired(agent.Id) })];
        IsPicking = crew.IsHiring;

        foreach (var panel in Panels)
        {
            panel.PropertyChanged += Follow;
        }

        Lock();
    }

    public IReadOnlyList<AgentPanel> Panels { get; }

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
        { IsPicking: true } => "crafter checks only the agents you mark.",
        { IsConfirming: true } => "crafter is checking the agents you marked.",
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
        if (changed.PropertyName == nameof(AgentPanel.IsMarked) && sender is AgentPanel panel)
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

        if (changed.PropertyName is nameof(AgentPanel.IsMarked) or nameof(AgentPanel.Readiness))
        {
            OnPropertyChanged(nameof(NeedsSetup));
            OnPropertyChanged(nameof(Headline));
            OnPropertyChanged(nameof(Explanation));
        }
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
