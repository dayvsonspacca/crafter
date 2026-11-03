using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationScreen : ObservableObject
{
    private const int Window = 20;

    private readonly Chat chat;

    private readonly IAgent agent;

    private readonly IChatKeeper keeper;

    private readonly Func<string, Task> copy;

    private readonly List<Message> earlier = [];

    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };

    private DateTimeOffset started;

    public ConversationScreen(Chat chat, IAgent agent, string projectName, IChatKeeper keeper, Func<string, Task> copy, string? resumed = null)
    {
        this.chat = chat;
        this.agent = agent;
        this.keeper = keeper;
        this.copy = copy;
        AgentName = agent.Name;
        ProjectName = projectName;
        Draft = string.Empty;
        Elapsed = string.Empty;
        Choosers = Choose();
        clock.Tick += (_, _) => Tick();
        Restore(resumed);
    }

    public event EventHandler? Concluded;

    public ObservableCollection<Message> Messages { get; } = [];

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
    public partial IReadOnlyList<ChatChooser> Choosers { get; set; }

    [ObservableProperty]
    public partial string Elapsed { get; set; }

    [ObservableProperty]
    public partial bool HasMessages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrafting), nameof(IsUnread), nameof(IsIdle))]
    public partial ChatState State { get; set; }

    [ObservableProperty]
    public partial bool IsInView { get; set; }

    public string? SessionId => chat.SessionId;

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
    private async Task Craft(CancellationToken stop)
    {
        if (IsCrafting || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var prompt = new Prompt(Draft.Trim());
        var answer = new CrafterMessage(copy, Retry);

        Draft = string.Empty;
        Messages.Add(new YourMessage(prompt.Text));
        Messages.Add(answer);
        Mark();
        HasMessages = true;
        State = ChatState.Crafting;
        started = DateTimeOffset.Now;
        Tick();
        clock.Start();

        var replies = chat.Ask(prompt, stop);
        OnPropertyChanged(nameof(Title));

        await foreach (var reply in replies)
        {
            if (reply is Began began)
            {
                Model = began.Model;
            }

            answer.Hear(reply);
        }

        clock.Stop();
        State = IsInView ? ChatState.Idle : ChatState.Unread;
        Concluded?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<ChatChooser> Choose() =>
        [.. agent.Settings.OfType<Choice>().Select(choice => new ChatChooser(choice, agent.Configuration, chat.Adjustments, Adjust))];

    private void Adjust(string key, string? value)
    {
        chat.Adjust(chat.Adjustments.With(key, value));
        Choosers = Choose();
        keeper.Keep(this);
    }

    private void Retry(CrafterMessage answer)
    {
        var place = Messages.IndexOf(answer);
        if (IsCrafting || place < 1 || Messages[place - 1] is not YourMessage asked)
        {
            return;
        }

        Draft = asked.Text;
        CraftCommand.Execute(null);
    }

    private void Tick()
    {
        var gone = DateTimeOffset.Now - started;
        Elapsed = gone.TotalMinutes < 1 ? $"crafting · {(int)gone.TotalSeconds}s" : $"crafting · {(int)gone.TotalMinutes}m {gone.Seconds}s";
    }

    private void Restore(string? resumed)
    {
        List<Message> told = [];
        foreach (var turn in chat.Turns)
        {
            switch (turn)
            {
                case Asked asked:
                    told.Add(new YourMessage(asked.Prompt.Text));
                    break;
                case Answered answered:
                    told.Add(new CrafterMessage(answered.Text, answered.Ending, copy, Retry));
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
            Messages.Add(new ResumedMark(resumed));
        }

        Mark();
        HasMessages = Messages.Count > 0;
    }

    private void Mark()
    {
        for (var at = 0; at < Messages.Count; at++)
        {
            Messages[at].IsLast = at == Messages.Count - 1 || Messages[at + 1] is ResumedMark && at + 1 == Messages.Count - 1;
        }
    }

    private static List<Message> Turns(List<Message> messages, int count)
    {
        var asks = 0;
        var from = messages.Count;

        while (from > 0 && asks < count)
        {
            from--;
            if (messages[from] is YourMessage)
            {
                asks++;
            }
        }

        return messages[from..];
    }
}
