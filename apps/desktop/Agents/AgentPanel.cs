using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Settings;

namespace Crafter.Desktop.Agents;

public sealed partial class AgentPanel : ObservableObject
{
    private readonly IAgent agent;

    private readonly IPreferencesStore store;

    private readonly Func<string, Task> copy;

    private Configuration configuration;

    public AgentPanel(IAgent agent, IPreferencesStore store, Func<string, Task> copy)
    {
        this.agent = agent;
        this.store = store;
        this.copy = copy;
        configuration = store.Load().ConfigurationOf(agent.Id);
        agent.Configure(configuration);
        Settings = [.. agent.Settings.Select(Present)];
    }

    public AgentId Id => agent.Id;

    public string AgentName => agent.Name;

    public string Summary => agent.Summary;

    public IReadOnlyList<object> Settings { get; }

    public bool IsReady => Readiness is Ready;

    public bool NeedsAttention => Readiness is Blocked;

    public string Detail => Readiness?.Detail ?? agent.Summary;

    public string State => IsChecking ? "Checking…" : Readiness?.State ?? string.Empty;

    public string Status => IsChecking
        ? "Checking…"
        : Readiness switch
        {
            null => "Not checked",
            { Detail.Length: 0 } => Readiness.State,
            _ => $"{Readiness.State} · {Readiness.Detail}",
        };

    public string? Remedy => (Readiness as Blocked)?.Remedy;

    public bool HasRemedy => Remedy is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsReady),
        nameof(NeedsAttention),
        nameof(Detail),
        nameof(State),
        nameof(Status),
        nameof(Remedy),
        nameof(HasRemedy))]
    public partial Readiness? Readiness { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(Status))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    public partial bool IsMarked { get; set; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [RelayCommand]
    private async Task Check()
    {
        IsChecking = true;
        Readiness = await agent.Check();
        IsChecking = false;
    }

    [RelayCommand]
    private Task CopyRemedy() => Remedy is { } remedy ? copy(remedy) : Task.CompletedTask;

    private object Present(Setting setting) => setting switch
    {
        Choice choice => new ChoiceSetting(choice, configuration, Change),
        Entry entry => new EntrySetting(entry, configuration, Change),
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Only choices and entries can be shown."),
    };

    private void Change(string key, string? value)
    {
        configuration = configuration.With(key, value);
        agent.Configure(configuration);
        store.Save(store.Load().Configure(agent.Id, configuration));
    }
}
