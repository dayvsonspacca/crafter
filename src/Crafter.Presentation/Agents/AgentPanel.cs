using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Preferences;
using Crafter.Presentation.Agents.Options;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Agents;

public sealed partial class AgentPanel : ObservableObject
{
    private readonly IAgent agent;

    private readonly PreferenceBook preferences;

    private readonly IClipboard clipboard;

    private Configuration configuration;

    public AgentPanel(IAgent agent, PreferenceBook preferences, IClipboard clipboard)
    {
        this.agent = agent;
        this.preferences = preferences;
        this.clipboard = clipboard;
        configuration = preferences.Current.ConfigurationOf(agent.Id);
        Settings = [.. agent.Settings(configuration).Select(Present)];
    }

    public AgentId Id => agent.Id;

    public string AgentName => agent.Name;

    public string Summary => agent.Summary;

    [ObservableProperty]
    public partial IReadOnlyList<object> Settings { get; set; }

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
        Readiness = await agent.Check(configuration);
        Settings = [.. agent.Settings(configuration).Select(Present)];
        IsChecking = false;
    }

    [RelayCommand]
    private Task CopyRemedy() => Remedy is { } remedy ? clipboard.Copy(remedy) : Task.CompletedTask;

    private object Present(Setting setting) => setting switch
    {
        Choice choice => new ChoiceSetting(choice, configuration, Change),
        Entry entry => new EntrySetting(entry, configuration, Change),
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Only choices and entries can be shown."),
    };

    private void Change(string key, string? value)
    {
        configuration = configuration.With(key, value);
        preferences.Configure(agent.Id, configuration);
    }
}
