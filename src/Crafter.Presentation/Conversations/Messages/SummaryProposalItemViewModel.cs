using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Crafter.Presentation.Conversations.Messages;

// A summary the agent proposes for the project, shown against the one it would replace. Like an
// axiom, it is always the user's to approve, whatever the chat's mode lets the agent do unasked.
public sealed partial class SummaryProposalItemViewModel(string agent, string project, string? current, string proposed) : ObservableObject, IAwaitsDecision
{
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Asking { get; } = string.IsNullOrWhiteSpace(current)
        ? $"{agent} proposes a summary for {project}"
        : $"{agent} proposes a new summary for {project}";

    public IReadOnlyList<SummaryLine> Lines { get; } = SummaryLine.Between(current, proposed);

    public bool IsOpen => Verdict is null;

    public Task<bool> Decision => decision.Task;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial string? Verdict { get; set; }

    public void Close(string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            decision.TrySetResult(false);
        }
    }

    [RelayCommand]
    private void Approve() => Decide(true, "Approved · the project keeps it now");

    [RelayCommand]
    private void Decline() => Decide(false, "Declined");

    private void Decide(bool approved, string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            decision.TrySetResult(approved);
        }
    }
}
