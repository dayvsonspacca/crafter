using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Crafter.Presentation.Conversations.Messages;

// An axiom the agent proposes for the project. Crafter asks here itself, whatever the chat's mode
// lets the agent do unasked: an axiom is always the user's to approve.
public sealed partial class AxiomProposalItemViewModel(string agent, string project, string axiom) : ObservableObject, IAwaitsDecision
{
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Asking { get; } = $"{agent} proposes an axiom for {project}";

    public string Axiom { get; } = axiom;

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
    private void Approve() => Decide(true, "Approved · the project holds it now");

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
