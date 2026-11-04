using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations;

public sealed partial class ApprovalItem(NeedsApproval approval, string agent, Action<IAwaitsDecision, Decision> decide) : ObservableObject, IAwaitsDecision
{
    public string Id { get; } = approval.Id;

    public string Asking { get; } = $"{agent} wants to use {approval.Tool}";

    public string Summary { get; } = approval.Summary;

    public string? Reason { get; } = approval.Reason;

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    public string? AlwaysLabel { get; } = approval.Remembers is { } remembered ? $"Always allow {remembered}" : null;

    public bool CanAlwaysAllow => AlwaysLabel is not null;

    public string FeedbackHint { get; } = $"If you deny, tell {agent} why (optional)";

    public bool IsOpen => Verdict is null;

    [ObservableProperty]
    public partial string? Feedback { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial string? Verdict { get; set; }

    public void Close(string verdict) => Verdict ??= verdict;

    [RelayCommand]
    private void Allow() => Decide(new Allow(), "Allowed");

    [RelayCommand]
    private void AlwaysAllow() => Decide(new AlwaysAllow(), $"Allowed · remembered {approval.Remembers}");

    [RelayCommand]
    private void Deny() =>
        Decide(new Deny(Feedback?.Trim() is { Length: > 0 } said ? said : null), Feedback?.Trim() is { Length: > 0 } why ? $"Denied · {why}" : "Denied");

    private void Decide(Decision decision, string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            decide(this, decision);
        }
    }
}
