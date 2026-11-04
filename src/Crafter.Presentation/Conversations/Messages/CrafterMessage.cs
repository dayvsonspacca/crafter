using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class CrafterMessage : Message
{
    private readonly IClipboard clipboard;

    private readonly Action<CrafterMessage> retry;

    private readonly string agent;

    private readonly Action<string, Decision> decide;

    public CrafterMessage(IClipboard clipboard, Action<CrafterMessage> retry, string agent, Action<string, Decision> decide)
    {
        this.clipboard = clipboard;
        this.retry = retry;
        this.agent = agent;
        this.decide = decide;
        Text = string.Empty;
        IsCrafting = true;
    }

    public CrafterMessage(string text, Ending ending, IClipboard clipboard, Action<CrafterMessage> retry)
        : this(clipboard, retry, string.Empty, (_, _) => { })
    {
        Hear(new Wrote(text));
        Hear(ending);
    }

    public ObservableCollection<object> Items { get; } = [];

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
                Items.Add(new ActivityItem(acting));
                break;
            case Acted acted:
                Items.OfType<ActivityItem>().LastOrDefault(item => item.Id == acted.Id)?.Finish(acted);
                break;
            case NeedsApproval approval:
                Items.Add(new ApprovalItem(approval, agent, Decided));
                IsWaiting = true;
                break;
            case NeedsAnswers asked:
                Items.Add(new QuestionItem(asked, agent, Decided));
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

    private TextSegment Writing()
    {
        if (Items.Count > 0 && Items[^1] is TextSegment segment)
        {
            return segment;
        }

        var started = new TextSegment(clipboard);
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
