using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class CrafterMessageViewModel : MessageViewModel
{
    private readonly IClipboard clipboard;

    private readonly Action<CrafterMessageViewModel> retry;

    private readonly string agent;

    private readonly Action<string, Decision> decide;

    public CrafterMessageViewModel(IClipboard clipboard, Action<CrafterMessageViewModel> retry, string agent, Action<string, Decision> decide)
    {
        this.clipboard = clipboard;
        this.retry = retry;
        this.agent = agent;
        this.decide = decide;
        Text = string.Empty;
        IsCrafting = true;
    }

    // An answer told again from the record: its steps as they came, or only its text when the
    // record is older than steps.
    public CrafterMessageViewModel(string text, IReadOnlyList<Reply> steps, Ending ending, IClipboard clipboard, Action<CrafterMessageViewModel> retry)
        : this(clipboard, retry, string.Empty, (_, _) => { })
    {
        foreach (var step in steps.Count > 0 ? steps : [new Wrote(text)])
        {
            Hear(step);
        }

        Hear(ending);
    }

    public ObservableCollection<object> Items { get; } = [];

    public bool IsWoken { get; init; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    public partial bool IsCrafting { get; set; }

    [ObservableProperty]
    public partial bool IsWaiting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote), nameof(IsDone))]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool HasFailed { get; set; }

    public bool HasNote => Note is not null;

    public bool IsDone => !IsCrafting && Note is null;

    public void Hear(Reply reply)
    {
        switch (reply)
        {
            case Wrote wrote when wrote.Fragment.Length > 0:
                Text += wrote.Fragment;
                Writing().Text += wrote.Fragment;
                break;
            case Acting acting:
                Items.Add(new ActivityItemViewModel(acting));
                break;
            case Acted acted:
                Items.OfType<ActivityItemViewModel>().LastOrDefault(item => item.Id == acted.Id)?.Finish(acted);
                break;
            case NeedsApproval approval:
                Items.Add(new ApprovalItemViewModel(approval, agent, Decided));
                IsWaiting = true;
                break;
            case NeedsAnswers asked:
                Items.Add(new QuestionItemViewModel(asked, agent, Decided));
                IsWaiting = true;
                break;
            case Withdrawn withdrawn:
                Items.OfType<IAwaitsDecision>().FirstOrDefault(item => item.Id == withdrawn.Id)?.Close("No longer needed");
                IsWaiting = Items.OfType<IAwaitsDecision>().Any(item => item.IsOpen);
                break;
            case Ending ending:
                IsCrafting = false;
                IsWaiting = false;
                HasFailed = ending is Failed;
                Note = NoteFor(ending);
                foreach (var item in Items.OfType<IAwaitsDecision>())
                {
                    item.Close("Not answered");
                }

                break;
        }
    }

    // Shows the proposal among the steps of this answer and waits for the user.
    public Task<bool> Propose(string project, string axiom) =>
        Await(new AxiomProposalItemViewModel(agent, project, axiom), proposal => proposal.Decision);

    public Task<bool> ProposeSummary(string project, string? current, string summary) =>
        Await(new SummaryProposalItemViewModel(agent, project, current, summary), proposal => proposal.Decision);

    private async Task<bool> Await<T>(T proposal, Func<T, Task<bool>> decision)
        where T : IAwaitsDecision
    {
        Items.Add(proposal);
        IsWaiting = true;
        var approved = await decision(proposal);
        IsWaiting = Items.OfType<IAwaitsDecision>().Any(each => each.IsOpen);

        return approved;
    }

    [RelayCommand]
    private Task Copy() => clipboard.Copy(Text);

    [RelayCommand]
    private void Retry() => retry(this);

    private TextSegmentViewModel Writing()
    {
        if (Items.Count > 0 && Items[^1] is TextSegmentViewModel segment)
        {
            return segment;
        }

        var started = new TextSegmentViewModel(clipboard, followsActivity: Items.Count > 0);
        Items.Add(started);

        return started;
    }

    private void Decided(IAwaitsDecision item, Decision decision)
    {
        IsWaiting = Items.OfType<IAwaitsDecision>().Any(each => each.IsOpen);
        decide(item.Id, decision);
    }

    private static string? NoteFor(Ending ending) => ending switch
    {
        Stopped => "Stopped",
        CutShort => "Cut short at the length limit",
        Refused { Reason: { } reason } => $"Declined ({reason})",
        Refused => "Declined",
        Failed failed => failed.Reason,
        _ => null,
    };
}
