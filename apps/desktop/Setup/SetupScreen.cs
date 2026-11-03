using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Settings;

namespace Crafter.Desktop.Setup;

public sealed partial class SetupScreen : ObservableObject
{
    private readonly Crew crew;

    private readonly IPreferencesStore store;

    public SetupScreen(Crew crew, IPreferencesStore store, Func<string, Task> copy)
    {
        this.crew = crew;
        this.store = store;
        crew.Hire(store.Load().Agents);
        Cards = [.. crew.Roster.Select(agent => new AgentCard(agent, copy) { IsMarked = crew.IsHired(agent.Id) })];
        IsPicking = crew.IsHiring;

        foreach (var card in Cards)
        {
            card.PropertyChanged += Follow;
        }

        Lock();
    }

    public IReadOnlyList<AgentCard> Cards { get; }

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
        { IsPicking: true } =>
            "crafter runs every chat through an agent installed on this machine. Mark the ones you use and crafter checks only those. You can change this later in Preferences.",
        { IsConfirming: true } => "crafter is checking the agents you marked.",
        _ => "crafter checked the agents you marked. Fix the ones below, then check again.",
    };

    private int Waiting => Cards.Count(card => card.IsMarked && card.NeedsAttention);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsSetup), nameof(Headline), nameof(Explanation))]
    public partial bool IsPicking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsSetup), nameof(Headline), nameof(Explanation))]
    public partial bool IsConfirming { get; set; }

    [RelayCommand]
    public Task Check() => Task.WhenAll(Cards.Where(card => card.IsMarked).Select(card => card.CheckCommand.ExecuteAsync(null)));

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task Continue()
    {
        Hire();
        IsConfirming = true;
        IsPicking = false;
        await Check();
        IsConfirming = false;
    }

    private bool CanContinue() => Cards.Any(card => card.IsMarked);

    [RelayCommand]
    private void ChangeAgents() => IsPicking = true;

    partial void OnIsPickingChanged(bool value) => Lock();

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(AgentCard.IsMarked) && sender is AgentCard card)
        {
            ContinueCommand.NotifyCanExecuteChanged();
            Lock();

            if (!IsPicking)
            {
                Hire();
                if (card.IsMarked)
                {
                    _ = card.CheckCommand.ExecuteAsync(null);
                }
            }
        }

        if (changed.PropertyName is nameof(AgentCard.IsMarked) or nameof(AgentCard.Readiness))
        {
            OnPropertyChanged(nameof(NeedsSetup));
            OnPropertyChanged(nameof(Headline));
            OnPropertyChanged(nameof(Explanation));
        }
    }

    private void Lock()
    {
        var marked = Cards.Count(card => card.IsMarked);

        foreach (var card in Cards)
        {
            card.IsLocked = !IsPicking && card.IsMarked && marked == 1;
        }
    }

    private void Hire()
    {
        List<AgentId> marked = [.. Cards.Where(card => card.IsMarked).Select(card => card.Id)];

        crew.Hire(marked);
        store.Save(store.Load() with { Agents = marked });
    }
}
