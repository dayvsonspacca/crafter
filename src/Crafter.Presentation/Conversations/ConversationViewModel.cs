using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations;

public sealed partial class ConversationViewModel : ObservableObject
{
    private const int Window = 20;

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly Chat chat;

    private readonly IAgent agent;

    private readonly PreferenceBook preferences;

    private readonly IChatKeeper keeper;

    private readonly IClipboard clipboard;

    private readonly TimeProvider time;

    private readonly SynchronizationContext? ui = SynchronizationContext.Current;

    private readonly List<MessageViewModel> earlier = [];

    private readonly Attachments attachments = new();

    private readonly IProjectFiles files;

    private readonly ITimer clock;

    private DateTimeOffset started;

    public ConversationViewModel(Chat chat, IAgent agent, PreferenceBook preferences, string projectName, IChatKeeper keeper, IClipboard clipboard, IProjectFiles files, TimeProvider time, string? resumed = null)
    {
        this.chat = chat;
        this.agent = agent;
        this.preferences = preferences;
        this.keeper = keeper;
        this.clipboard = clipboard;
        this.files = files;
        this.time = time;
        Mentions = new MentionsViewModel(files);
        AgentName = agent.Name;
        ProjectName = projectName;
        Draft = string.Empty;
        Elapsed = string.Empty;
        Choosers = Choose();
        clock = time.CreateTimer(_ => OnUi(Tick), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Restore(resumed);
    }

    public event EventHandler? Concluded;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public ObservableCollection<AttachmentViewModel> Attached { get; } = [];

    public MentionsViewModel Mentions { get; }

    public bool HasAttached => Attached.Count > 0;

    [ObservableProperty]
    public partial string ProjectName { get; set; }

    public string AgentName { get; }

    public string Title => chat.Title.Text;

    public bool IsCrafting => State == ChatState.Crafting;

    public bool IsUnread => State == ChatState.Unread;

    public bool IsIdle => State == ChatState.Idle;

    public bool HasEarlier => earlier.Count > 0;

    public string EarlierLabel => $"Show {earlier.Count} earlier messages";

    public bool HasModel => Model is not null;

    [ObservableProperty]
    public partial string Draft { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModel))]
    public partial string? Model { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChatChooserViewModel> Choosers { get; set; }

    [ObservableProperty]
    public partial string Elapsed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsClock))]
    public partial bool IsWaiting { get; set; }

    public bool ShowsClock => IsCrafting && !IsWaiting;

    [ObservableProperty]
    public partial bool HasMessages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrafting), nameof(IsUnread), nameof(IsIdle), nameof(ShowsClock))]
    public partial ChatState State { get; set; }

    [ObservableProperty]
    public partial bool IsInView { get; set; }

    public string? SessionId => chat.SessionId;

    private Configuration Preferred => preferences.Current.ConfigurationOf(agent.Id);

    public ChatRecord Record() => chat.Record();

    public void Leave()
    {
        if (CraftCommand.IsRunning)
        {
            CraftCommand.Cancel();
        }
    }

    public async Task LeaveAsync()
    {
        Leave();

        if (CraftCommand.ExecutionTask is { } running)
        {
            await running;
        }
    }

    public string Paste(string text) => attachments.Paste(text);

    public void Attach(Picture picture)
    {
        attachments.Attach(picture);
        Attached.Add(new AttachmentViewModel(picture, Detach));
        OnPropertyChanged(nameof(HasAttached));
    }

    public void DetachLast()
    {
        if (Attached.Count > 0)
        {
            Detach(Attached[^1]);
        }
    }

    public string Mention(string path)
    {
        var relative = Path.GetRelativePath(files.Folder, path);
        var inside = !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        var shown = (inside ? relative : path).Replace(Path.DirectorySeparatorChar, '/');

        return $"@{(shown.Any(char.IsWhiteSpace) ? $"\"{shown}\"" : shown)} ";
    }

    partial void OnIsInViewChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        Choosers = Choose();
        if (State == ChatState.Unread)
        {
            State = ChatState.Idle;
        }
    }

    [RelayCommand]
    private void Select() => keeper.Select(this);

    [RelayCommand]
    private void Close() => keeper.Close(this);

    [RelayCommand]
    private void ShowEarlier()
    {
        var shown = Turns(earlier, Window);
        earlier.RemoveRange(earlier.Count - shown.Count, shown.Count);

        for (var at = shown.Count - 1; at >= 0; at--)
        {
            Messages.Insert(0, shown[at]);
        }

        OnPropertyChanged(nameof(HasEarlier));
        OnPropertyChanged(nameof(EarlierLabel));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Craft(YourMessageViewModel? again, CancellationToken stop)
    {
        if (IsCrafting || (again is null ? attachments.Compose(Draft) : again.Prompt) is not { } prompt)
        {
            return;
        }

        var asked = again is null ? new YourMessageViewModel(prompt, Draft.Trim()) : new YourMessageViewModel(prompt, again.Text);
        var answer = new CrafterMessageViewModel(clipboard, Retry, AgentName, chat.Decide);
        answer.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(CrafterMessageViewModel.IsWaiting))
            {
                IsWaiting = answer.IsWaiting;
            }
        };

        if (again is null)
        {
            Draft = string.Empty;
            attachments.Clear();
            Attached.Clear();
            OnPropertyChanged(nameof(HasAttached));
            Mentions.Close();
        }

        Messages.Add(asked);
        Messages.Add(answer);
        Mark();
        HasMessages = true;
        State = ChatState.Crafting;
        started = time.GetUtcNow();
        Tick();
        clock.Change(Second, Second);

        var replies = chat.Ask(prompt, Preferred, stop);
        OnPropertyChanged(nameof(Title));

        await foreach (var reply in replies)
        {
            if (reply is Began began)
            {
                Model = began.Model;
            }

            answer.Hear(reply);
        }

        clock.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        IsWaiting = false;
        State = IsInView ? ChatState.Idle : ChatState.Unread;
        Concluded?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<ChatChooserViewModel> Choose() =>
        [.. agent.Settings(Preferred).OfType<Choice>().Select(choice => new ChatChooserViewModel(choice, Preferred, chat.Adjustments, Adjust))];

    private void Adjust(string key, string? value)
    {
        chat.Adjust(chat.Adjustments.With(key, value));
        Choosers = Choose();
        keeper.Keep(this);
    }

    private void Retry(CrafterMessageViewModel answer)
    {
        var place = Messages.IndexOf(answer);
        if (IsCrafting || place < 1 || Messages[place - 1] is not YourMessageViewModel asked)
        {
            return;
        }

        CraftCommand.Execute(asked);
    }

    private void Detach(AttachmentViewModel attachment)
    {
        attachments.Detach(attachment.Picture);
        Attached.Remove(attachment);
        OnPropertyChanged(nameof(HasAttached));
    }

    private void Tick()
    {
        var gone = time.GetUtcNow() - started;
        Elapsed = gone.TotalMinutes < 1 ? $"crafting · {(int)gone.TotalSeconds}s" : $"crafting · {(int)gone.TotalMinutes}m {gone.Seconds}s";
    }

    private void OnUi(Action action)
    {
        if (ui is null)
        {
            action();
        }
        else
        {
            ui.Post(_ => action(), null);
        }
    }

    private void Restore(string? resumed)
    {
        List<MessageViewModel> told = [];
        foreach (var turn in chat.Turns)
        {
            switch (turn)
            {
                case Asked asked:
                    told.Add(new YourMessageViewModel(asked.Prompt));
                    break;
                case Answered answered:
                    told.Add(new CrafterMessageViewModel(answered.Text, answered.Ending, clipboard, Retry));
                    break;
            }
        }

        var shown = Turns(told, Window);
        earlier.AddRange(told[..^shown.Count]);
        foreach (var message in shown)
        {
            Messages.Add(message);
        }

        if (resumed is not null && told.Count > 0)
        {
            Messages.Add(new ResumedMarkViewModel(resumed));
        }

        Mark();
        HasMessages = Messages.Count > 0;
    }

    private void Mark()
    {
        for (var at = 0; at < Messages.Count; at++)
        {
            Messages[at].IsLast = at == Messages.Count - 1 || Messages[at + 1] is ResumedMarkViewModel && at + 1 == Messages.Count - 1;
        }
    }

    private static List<MessageViewModel> Turns(List<MessageViewModel> messages, int count)
    {
        var asks = 0;
        var from = messages.Count;

        while (from > 0 && asks < count)
        {
            from--;
            if (messages[from] is YourMessageViewModel)
            {
                asks++;
            }
        }

        return messages[from..];
    }
}
