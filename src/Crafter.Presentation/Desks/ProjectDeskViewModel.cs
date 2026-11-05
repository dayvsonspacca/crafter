using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Desks;

public sealed partial class ProjectDeskViewModel : ObservableObject, IChatKeeper
{
    private readonly IChatStore store;

    private readonly Crew crew;

    private readonly PreferenceBook preferences;

    private readonly IClipboard clipboard;

    private readonly TimeProvider time;

    private readonly IProjectFiles files;

    private readonly Func<IReadOnlyList<Skill>>? skills;

    public ProjectDeskViewModel(
        Project project,
        IChatStore store,
        Crew crew,
        PreferenceBook preferences,
        ProjectDetailsViewModel? details,
        ProjectBriefViewModel? brief,
        IClipboard clipboard,
        TimeProvider time,
        Func<IReadOnlyList<Skill>>? skills = null)
    {
        this.skills = skills;
        Project = project;
        this.store = store;
        this.crew = crew;
        this.preferences = preferences;
        this.clipboard = clipboard;
        this.time = time;
        files = new FilesOnDisk(project.Folder);

        foreach (var record in store.Load(project))
        {
            var agent = crew.Find(record.Agent);
            var conversation = new Conversation(agent.Begin(project, record.SessionId), record.Turns);
            Add(new Chat(record.Id, agent.Id, record.Title, conversation), agent);
        }

        Overview = new ProjectOverviewViewModel(this, crew, details, brief, time);
    }

    public Project Project { get; private set; }

    public ProjectOverviewViewModel Overview { get; }

    public bool ShowsOverview => Selected is null;

    public ObservableCollection<ConversationViewModel> Chats { get; } = [];

    public int Count => Chats.Count;

    public bool IsCrafting => Chats.Any(chat => chat.State == ChatState.Crafting);

    public bool HasJobs => Chats.Any(chat => chat.HasJobs);

    public bool HasUnread => Chats.Any(chat => chat.State == ChatState.Unread);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOverview))]
    public partial ConversationViewModel? Selected { get; set; }

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

    public void Select(ConversationViewModel chat) => Selected = chat;

    // Alt+1 to 9: the open chats in tab order; a place with no chat does nothing.
    public void SelectAt(int place)
    {
        if (place >= 0 && place < Chats.Count)
        {
            Selected = Chats[place];
        }
    }

    public void Keep(ConversationViewModel chat) => Save();

    public void Describe(Project described)
    {
        Project = described;
        Overview.Describe(described);
        foreach (var chat in Chats)
        {
            chat.ProjectName = described.Name.Text;
        }
    }

    public void Close(ConversationViewModel chat)
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
        NotifyDerived();
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

    partial void OnSelectedChanged(ConversationViewModel? oldValue, ConversationViewModel? newValue)
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

    private ConversationViewModel Add(Chat chat, IAgent agent, string? resumed = null)
    {
        var viewModel = new ConversationViewModel(chat, agent, preferences, Project.Name.Text, this, clipboard, files, time, resumed, skills);
        viewModel.PropertyChanged += Follow;
        viewModel.Concluded += Keep;
        Chats.Add(viewModel);
        NotifyDerived();

        return viewModel;
    }

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(ConversationViewModel.State))
        {
            NotifyDerived();
        }
    }

    private void Keep(object? sender, EventArgs e) => Save();

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsCrafting));
        OnPropertyChanged(nameof(HasUnread));
    }

    private void Save() => store.Save(Project, [.. Chats.Select(chat => chat.Record())]);
}
