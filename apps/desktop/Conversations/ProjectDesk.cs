using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Desktop.Conversations;

public sealed partial class ProjectDesk : ObservableObject, IChatKeeper
{
    private readonly IChatArchive archive;

    private readonly Crew crew;

    public ProjectDesk(Project project, IChatArchive archive, Crew crew)
    {
        Project = project;
        this.archive = archive;
        this.crew = crew;

        foreach (var record in archive.Load(project))
        {
            var agent = crew.Find(record.Agent);
            Add(new Chat(record.Id, agent.Id, record.Title, new Conversation(agent.Begin(project, record.SessionId), record.Turns)), agent);
        }

        if (Chats.Count == 0)
        {
            NewChat();
        }
        else
        {
            Selected = Chats[0];
        }
    }

    public Project Project { get; private set; }

    public ObservableCollection<ConversationScreen> Chats { get; } = [];

    public int Count => Chats.Count;

    public bool IsCrafting => Chats.Any(chat => chat.State == ChatState.Crafting);

    public bool HasUnread => Chats.Any(chat => chat.State == ChatState.Unread);

    [ObservableProperty]
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

    public void Select(ConversationScreen chat) => Selected = chat;

    public void Rename(Project renamed)
    {
        Project = renamed;
        foreach (var chat in Chats)
        {
            chat.ProjectName = renamed.Name.Text;
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

        if (Chats.Count == 0)
        {
            NewChat();
        }
        else
        {
            if (Selected == chat)
            {
                Selected = Chats[Math.Min(place, Chats.Count - 1)];
            }

            Save();
        }

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
        if (Chats.Count < 2 || Selected is null)
        {
            return;
        }

        var place = Chats.IndexOf(Selected);
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

    private ConversationScreen Add(Chat chat, IAgent agent)
    {
        var screen = new ConversationScreen(chat, agent, Project.Name.Text, this);
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
