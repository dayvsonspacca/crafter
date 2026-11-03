using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Desktop.Platform;

namespace Crafter.Desktop.Conversations;

public sealed partial class ProjectDesk : ObservableObject, IChatKeeper
{
    private readonly IChatArchive archive;

    private readonly Crew crew;

    private readonly IClipboard clipboard;

    private readonly TimeProvider time;

    public ProjectDesk(Project project, IChatArchive archive, Crew crew, Func<Project, Task> edit, IClipboard clipboard, TimeProvider time)
    {
        Project = project;
        this.archive = archive;
        this.crew = crew;
        this.clipboard = clipboard;
        this.time = time;

        foreach (var record in archive.Load(project))
        {
            var agent = crew.Find(record.Agent);
            var session = agent.Begin(project, record.SessionId);
            session.Adjust(record.Adjustments);
            Add(new Chat(record.Id, agent.Id, record.Title, new Conversation(session, record.Turns)), agent);
        }

        Overview = new ProjectOverview(this, crew, edit, time);
    }

    public Project Project { get; private set; }

    public ProjectOverview Overview { get; }

    public bool ShowsOverview => Selected is null;

    public ObservableCollection<ConversationScreen> Chats { get; } = [];

    public int Count => Chats.Count;

    public bool IsCrafting => Chats.Any(chat => chat.State == ChatState.Crafting);

    public bool HasUnread => Chats.Any(chat => chat.State == ChatState.Unread);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOverview))]
    public partial ConversationScreen? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    [RelayCommand]
    public void NewChat()
    {
        var agent = crew.Default;
        Selected = Add(new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(agent.Begin(Project))), agent);
        Save();
    }

    [RelayCommand]
    public void ShowOverview()
    {
        Selected = null;
        _ = Overview.Refresh();
    }

    public async Task Resume(IAgent agent, PastChat past)
    {
        if (Chats.FirstOrDefault(chat => chat.SessionId == past.SessionId) is { } open)
        {
            Selected = open;

            return;
        }

        var turns = agent is IKeepsHistory history ? await history.Replay(Project, past.SessionId) : [];
        var resumed = $"resumed from {agent.Name} · {Elapsed.Since(past.LastActive, time.GetLocalNow())}";
        Selected = Add(new Chat(ChatId.New(), agent.Id, past.Title, new Conversation(agent.Begin(Project, past.SessionId), turns)), agent, resumed);
        Save();
    }

    public void Select(ConversationScreen chat) => Selected = chat;

    public void Keep(ConversationScreen chat) => Save();

    public void Describe(Project described)
    {
        Project = described;
        Overview.Describe(described);
        foreach (var chat in Chats)
        {
            chat.ProjectName = described.Name.Text;
        }
    }

    public void Close(ConversationScreen chat)
    {
        var place = Chats.IndexOf(chat);
        if (place < 0)
        {
            return;
        }

        chat.Leave();
        chat.PropertyChanged -= Follow;
        chat.Concluded -= Keep;
        Chats.RemoveAt(place);

        if (Selected == chat)
        {
            Selected = Chats.Count > 0 ? Chats[Math.Min(place, Chats.Count - 1)] : null;
        }

        if (Selected is null)
        {
            _ = Overview.Refresh();
        }

        Save();
        Notify();
    }

    public void CloseSelected()
    {
        if (Selected is { } chat)
        {
            Close(chat);
        }
    }

    public void Next(int step)
    {
        if (Chats.Count == 0)
        {
            return;
        }

        var place = Selected is null ? (step > 0 ? -1 : Chats.Count) : Chats.IndexOf(Selected);
        Selected = Chats[(place + step + Chats.Count) % Chats.Count];
    }

    public void Leave()
    {
        foreach (var chat in Chats)
        {
            chat.Leave();
        }
    }

    public Task LeaveAsync() => Task.WhenAll(Chats.Select(chat => chat.LeaveAsync()));

    partial void OnSelectedChanged(ConversationScreen? oldValue, ConversationScreen? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsInView = false;
        }

        if (newValue is not null)
        {
            newValue.IsInView = IsShown;
        }
    }

    partial void OnIsShownChanged(bool value)
    {
        if (Selected is { } chat)
        {
            chat.IsInView = value;
        }
    }

    private ConversationScreen Add(Chat chat, IAgent agent, string? resumed = null)
    {
        var screen = new ConversationScreen(chat, agent, Project.Name.Text, this, clipboard, time, resumed);
        screen.PropertyChanged += Follow;
        screen.Concluded += Keep;
        Chats.Add(screen);
        Notify();

        return screen;
    }

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(ConversationScreen.State))
        {
            Notify();
        }
    }

    private void Keep(object? sender, EventArgs e) => Save();

    private void Notify()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsCrafting));
        OnPropertyChanged(nameof(HasUnread));
    }

    private void Save() => archive.Save(Project, [.. Chats.Select(chat => chat.Record())]);
}
