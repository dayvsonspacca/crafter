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

    public CrafterMessageViewModel(string text, Ending ending, IClipboard clipboard, Action<CrafterMessageViewModel> retry)
        : this(clipboard, retry, string.Empty, (_, _) => { })
    {
        Hear(new Wrote(text));
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
        Refused { Category: { } category } => $"Declined ({category})",
        Refused => "Declined",
        Failed failed => failed.Reason,
        _ => null,
    };
}
