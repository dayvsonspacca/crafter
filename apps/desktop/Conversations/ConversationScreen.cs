using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationScreen : ObservableObject
{
    private readonly Chat chat;

    private readonly IChatKeeper keeper;

    public ConversationScreen(Chat chat, IAgent agent, string projectName, IChatKeeper keeper)
    {
        this.chat = chat;
        this.keeper = keeper;
        AgentName = agent.Name;
        ProjectName = projectName;
        Draft = string.Empty;
        Model = agent.Name;
        Restore();
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

    [ObservableProperty]
    public partial string Draft { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; }

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
        if (value && State == ChatState.Unread)
        {
            State = ChatState.Idle;
        }
    }

    [RelayCommand]
    private void Select() => keeper.Select(this);

    [RelayCommand]
    private void Close() => keeper.Close(this);

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Craft(CancellationToken stop)
    {
        if (IsCrafting || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var prompt = new Prompt(Draft.Trim());
        var answer = new CrafterMessage();

        Draft = string.Empty;
        Messages.Add(new YourMessage(prompt.Text));
        Messages.Add(answer);
        HasMessages = true;
        State = ChatState.Crafting;

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

        State = IsInView ? ChatState.Idle : ChatState.Unread;
        Concluded?.Invoke(this, EventArgs.Empty);
    }

    private void Restore()
    {
        foreach (var turn in chat.Turns)
        {
            switch (turn)
            {
                case Asked asked:
                    Messages.Add(new YourMessage(asked.Prompt.Text));
                    break;
                case Answered answered:
                    Messages.Add(new CrafterMessage(answered.Text, answered.Ending));
                    break;
            }
        }

        HasMessages = Messages.Count > 0;
    }
}
