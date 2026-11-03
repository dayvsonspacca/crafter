using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ApprovalItem(NeedsApproval approval, string agent, Action<ApprovalItem, Decision> decide) : ObservableObject
{
    public string Id { get; } = approval.Id;

    public string Asking { get; } = $"{agent} wants to use {approval.Tool}";

    public string Summary { get; } = approval.Summary;

    public string? Reason { get; } = approval.Reason;

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    public string TrustLabel { get; } = $"Allow {approval.Tool} in this chat";

    public bool IsOpen => Verdict is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial string? Verdict { get; set; }

    public void Close(string verdict) => Verdict ??= verdict;

    [RelayCommand]
    private void Allow() => Decide(new Allow(), "Allowed");

    [RelayCommand]
    private void AllowTool() => Decide(new AllowTool(), "Allowed in this chat");

    [RelayCommand]
    private void Deny() => Decide(new Deny(), "Denied");

    private void Decide(Decision decision, string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            decide(this, decision);
        }
    }
}
