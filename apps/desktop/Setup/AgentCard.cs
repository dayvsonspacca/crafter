using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;

namespace Crafter.Desktop.Setup;

public sealed partial class AgentCard(IAgent agent, Func<string, Task> copy) : ObservableObject
{
    public AgentId Id => agent.Id;

    public string AgentName => agent.Name;

    public bool IsReady => Readiness is Ready;

    public bool NeedsAttention => Readiness is not null and not Ready;

    public string Detail => Readiness switch
    {
        Missing => "Not installed on this machine. Install it from a terminal:",
        SignedOut => "Installed but signed out. Sign in once from a terminal:",
        Broken broken => broken.Reason,
        _ => agent.Summary,
    };

    public string State => IsChecking
        ? "Checking…"
        : Readiness switch
        {
            Ready => "Ready",
            Missing => "Not installed",
            SignedOut => "Signed out",
            Broken => "Not working",
            _ => string.Empty,
        };

    public string Status => IsChecking
        ? "Checking…"
        : Readiness switch
        {
            Ready { Account: { } account } ready => $"Version {ready.Version} · {account}",
            Ready ready => $"Version {ready.Version}",
            Missing => "Not installed",
            SignedOut => "Signed out",
            Broken broken => broken.Reason,
            _ => "Not checked",
        };

    public string? Command => Readiness switch
    {
        Missing missing => missing.Install,
        SignedOut signedOut => signedOut.SignIn,
        _ => null,
    };

    public bool HasCommand => Command is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsReady),
        nameof(NeedsAttention),
        nameof(Detail),
        nameof(State),
        nameof(Status),
        nameof(Command),
        nameof(HasCommand))]
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
    private Task CopyCommand() => Command is { } command ? copy(command) : Task.CompletedTask;
}
