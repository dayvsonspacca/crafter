using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Keyboard;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Todos;

namespace Crafter.Presentation;

public sealed partial class WorkbenchViewModel : ObservableObject, IShelfKeeper, IProjectEditor, ITodoKeeper
{
    private readonly Bookshelf bookshelf;

    private readonly PreferenceBook preferences;

    private readonly DeskFactory deskFactory;

    private readonly IClipboard clipboard;

    private readonly Dictionary<Project, ProjectDeskViewModel> desks = [];

    // Home is Crafter's own chat, in a folder of Crafter's: it is not on the shelf, cannot be
    // edited, closed or removed, and is where Crafter opens.
    public WorkbenchViewModel(
        Project home,
        Bookshelf bookshelf,
        PreferenceBook preferences,
        FolderPickerViewModel picker,
        ProjectNamerViewModel namer,
        SetupViewModel setup,
        PreferencesViewModel preferencesScreen,
        DeskFactory deskFactory,
        IClipboard clipboard,
        StatusBarViewModel status)
    {
        this.bookshelf = bookshelf;
        this.preferences = preferences;
        this.deskFactory = deskFactory;
        this.clipboard = clipboard;
        Home = home;
        HomeItem = new ShelfItemViewModel(home, ProjectShortcuts.Home, this);
        Picker = picker;
        Namer = namer;
        Setup = setup;
        Preferences = preferencesScreen;
        Status = status;
        Preferences.Library.Changed += (_, _) =>
        {
            foreach (var desk in desks.Values)
            {
                desk.Aside?.Brief.Stock();
            }
        };
        Preferences.PropertyChanged += Follow;
        Setup.PropertyChanged += Follow;
        if (!Setup.IsPicking)
        {
            _ = Setup.Check();
        }

        IsSidebarVisible = true;
        IsAsideVisible = true;
        Restock();
        Open(Home);
    }

    public Project Home { get; }

    public ShelfItemViewModel HomeItem { get; }

    public FolderPickerViewModel Picker { get; }

    public ProjectNamerViewModel Namer { get; }

    public PreferencesViewModel Preferences { get; }

    public SetupViewModel Setup { get; }

    public StatusBarViewModel Status { get; }

    public ObservableCollection<ShelfItemViewModel> Shelf { get; } = [];

    public bool IsShowingPreferences => Preferences.IsOpen;

    public bool ShowsConversation => Desk is not null;

    public bool ShowsSetup => Setup.NeedsSetup;

    public bool IsCrafting => desks.Values.Any(desk => desk.IsCrafting);

    public bool IsBusy => desks.Values.Any(desk => desk.IsCrafting || desk.HasJobs);

    [ObservableProperty]
    public partial bool HasProjects { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSidebar))]
    public partial bool IsSidebarVisible { get; set; }

    public bool ShowsSidebar => IsSidebarVisible;

    // The panel on the right, with the todos, skills and axioms of the project in front.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAside))]
    public partial bool IsAsideVisible { get; set; }

    public bool ShowsAside => IsAsideVisible && Desk?.Aside is not null;

    public Project? Current
    {
        get;
        private set
        {
            field = value;
            Status.Watch(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Heading));
        }
    }

    public string? Heading => ShowsSetup ? "Setup" : Current?.Name.Text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsConversation), nameof(ShowsSidebar), nameof(ShowsAside))]
    public partial ProjectDeskViewModel? Desk { get; set; }

    public void OpenChat(int place) => Desk?.SelectAt(place);

    public void OpenSlot(int place)
    {
        if (place < Shelf.Count)
        {
            Open(Shelf[place].Project);
        }
    }

    public Task LeaveAsync() => Task.WhenAll(desks.Values.Select(desk => desk.LeaveAsync()));

    [RelayCommand]
    public void Open(Project project)
    {
        Preferences.IsOpen = false;
        if (project == Current)
        {
            Desk?.ShowOverview();

            return;
        }

        if (Desk is { } previous)
        {
            previous.IsShown = false;
        }

        if (project != Home)
        {
            bookshelf.Open(project);
        }

        if (!desks.TryGetValue(project, out var desk))
        {
            desk = project == Home
                ? deskFactory.Make(project, skills: () => SkillsFor(project))
                : deskFactory.Make(
                    project,
                    new ProjectDetailsViewModel(project, this, Picker),
                    new ProjectAsideViewModel(
                        new TodoListViewModel(project, this),
                        new ProjectBriefViewModel(project, Preferences.Library, (shown, skill) => bookshelf.Has(shown, skill.Id), Give, AxiomsOf, Hold, Drop)),
                    () => SkillsFor(project));
            desks[project] = desk;
        }

        Current = project;
        Desk = desk;
        desk.IsShown = true;

        // Coming back to a project returns to the chat that was in front; a project without
        // one opens on its overview, fresh.
        if (desk.Selected is null)
        {
            desk.ShowOverview();
        }

        Restock();
    }

    // Closing a project ends its chats and goes back Home, which never closes.
    [RelayCommand]
    public void Close()
    {
        if (Desk is not { } desk || desk.Project == Home)
        {
            return;
        }

        desk.Leave();
        desk.IsShown = false;
        desks.Remove(desk.Project);
        Desk = null;
        Current = null;
        Open(Home);
    }

    [RelayCommand]
    public void Remove(Project project)
    {
        if (project == Home)
        {
            return;
        }

        if (project == Current)
        {
            Close();
        }
        else if (desks.Remove(project, out var desk))
        {
            desk.Leave();
        }

        bookshelf.Remove(project);
        Restock();
    }

    public Task CopyPath(Project project) => clipboard.Copy(project.Folder);

    // A project is edited on its own page, in its Details tab.
    public Task Edit(Project project)
    {
        if (project != Home)
        {
            Open(project);
            Show(ProjectTab.Details);
        }

        return Task.CompletedTask;
    }

    public AgentInstructions? InstructionsIn(Project project) => bookshelf.InstructionsIn(project);

    public Project Save(Project project, ProjectName name, IconChange icon, string? summary)
    {
        if (summary is not null)
        {
            bookshelf.Summarize(project, summary);
        }

        return Apply(project, name, icon);
    }

    public IReadOnlyList<Project> Projects => bookshelf.Projects;

    // Where new projects go, as Preferences sets it.
    public string ProjectsRoot => preferences.Current.ProjectsRoot;

    public ProjectName NameOf(string folder) => bookshelf.NameOf(folder);

    // Puts a folder in the sidebar without leaving the project in front.
    public Project Shelve(string folder, ProjectName name)
    {
        var added = bookshelf.Add(folder, name);
        Restock();

        return added;
    }

    public Project Rename(Project project, ProjectName name) => Apply(project, name, new SameIcon());

    public Project ChangeIcon(Project project, string image) => Apply(project, project.Name, new NewIcon(image));

    // The library skills a project's agent gets: this Crafter's defaults and the ones given to it.
    public IReadOnlyList<Skill> SkillsFor(Project project) =>
        [.. Preferences.Library.Skills.Where(skill => Preferences.Library.IsDefault(skill) || bookshelf.Has(project, skill.Id))];

    public IReadOnlyList<string> AxiomsOf(Project project) => bookshelf.AxiomsOf(project);

    public string? SummaryOf(Project project) => bookshelf.SummaryOf(project);

    // A new summary reaches the project's chats when their sessions start again.
    public bool Summarize(Project project, string summary)
    {
        var changed = bookshelf.Summarize(project, summary);
        if (changed && desks.GetValueOrDefault(project)?.Overview.Details is { } details)
        {
            details.Describe(project);
        }

        return changed;
    }

    // A new axiom reaches the project's chats when their sessions start again.
    public bool Hold(Project project, string axiom) => Changed(project, bookshelf.Hold(project, axiom));

    public bool Drop(Project project, string axiom) => Changed(project, bookshelf.Drop(project, axiom));

    public void Give(Project project, Skill skill, bool given) => bookshelf.Give(project, skill.Id, given);

    public IReadOnlyList<Todo> TodosOf(Project project) => bookshelf.TodosOf(project);

    public Todo? Jot(Project project, string text)
    {
        var jotted = bookshelf.Jot(project, text);
        if (jotted is not null)
        {
            Retodo(project);
        }

        return jotted;
    }

    public bool Tick(Project project, int number, bool done) => Retodo(project, bookshelf.Tick(project, number, done));

    public bool Scrap(Project project, int number) => Retodo(project, bookshelf.Scrap(project, number));

    // A todo cited with no chat in front starts one for it.
    public void Cite(Project project, Todo todo)
    {
        if (!desks.TryGetValue(project, out var desk))
        {
            return;
        }

        if (desk.Selected is null)
        {
            desk.NewChat();
        }

        desk.Selected?.Cite(todo);
    }

    private bool Changed(Project project, bool changed)
    {
        if (changed)
        {
            desks.GetValueOrDefault(project)?.Aside?.Brief.Recall();
        }

        return changed;
    }

    private bool Retodo(Project project, bool changed = true)
    {
        if (changed)
        {
            desks.GetValueOrDefault(project)?.Aside?.Todos.Recall();
        }

        return changed;
    }

    public int OpenChats(Project project) => desks.GetValueOrDefault(project)?.Count ?? 0;

    // The open chat an agent session runs, to ask the user there.
    public ConversationViewModel? ChatRunning(IAgentSession session) =>
        desks.Values.SelectMany(desk => desk.Chats).FirstOrDefault(chat => chat.Runs(session));

    private Project Apply(Project project, ProjectName name, IconChange icon)
    {
        var edited = bookshelf.Edit(project, name, icon);
        if (desks.TryGetValue(edited, out var desk))
        {
            desk.Describe(edited);
        }

        if (edited == Current)
        {
            Current = edited;
        }

        Restock();

        return edited;
    }

    [RelayCommand]
    private async Task Add()
    {
        var start = preferences.Current.ProjectsRoot;

        while (await Picker.Pick("Add Project", start) is { } folder)
        {
            if (bookshelf.Projects.FirstOrDefault(project => project == new Project(folder)) is { } added)
            {
                Open(added);

                return;
            }

            switch (await Namer.Add(folder, bookshelf.NameOf(folder)))
            {
                case Named named:
                    Open(bookshelf.Add(folder, named.Name));
                    return;
                case WentBack:
                    start = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) ?? folder;
                    break;
                default:
                    return;
            }
        }
    }

    [RelayCommand]
    private void GoHome() => Open(Home);

    // Brings the page of the project in front forward, on one of its tabs.
    private void Show(ProjectTab tab)
    {
        if (Desk is { } desk && desk.Project != Home)
        {
            desk.ShowOverview();
            desk.Overview.Show(tab);
        }
    }

    [RelayCommand]
    private Task EditCurrent() => Current is { } project ? Edit(project) : Task.CompletedTask;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    private void ToggleAside() => IsAsideVisible = !IsAsideVisible;

    [RelayCommand]
    private void TogglePreferences() => Preferences.IsOpen = !Preferences.IsOpen;

    [RelayCommand]
    private void NewChat() => Desk?.NewChat();

    [RelayCommand]
    private void CloseChat() => Desk?.CloseSelected();

    [RelayCommand]
    private void NextChat() => Desk?.Next(1);

    [RelayCommand]
    private void ToggleJobs() => Desk?.Selected?.ToggleJobsCommand.Execute(null);

    [RelayCommand]
    private void PreviousChat() => Desk?.Next(-1);

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName is nameof(PreferencesViewModel.IsOpen) or nameof(SetupViewModel.NeedsSetup))
        {
            NotifyDerived();
        }
    }

    partial void OnDeskChanged(ProjectDeskViewModel? oldValue, ProjectDeskViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= FollowDesk;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += FollowDesk;
        }

        Status.Chat = newValue?.Selected;
    }

    // The status bar follows the chat in front: its jobs sit there.
    private void FollowDesk(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName is nameof(ProjectDeskViewModel.Selected))
        {
            Status.Chat = Desk?.Selected;
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(IsShowingPreferences));
        OnPropertyChanged(nameof(ShowsConversation));
        OnPropertyChanged(nameof(ShowsSetup));
        OnPropertyChanged(nameof(Heading));
    }

    private void Restock()
    {
        var projects = bookshelf.Projects;

        Shelf.Clear();
        for (var place = 0; place < projects.Count; place++)
        {
            Shelf.Add(new ShelfItemViewModel(projects[place], ProjectShortcuts.Slot(place), this));
        }

        HasProjects = Shelf.Count > 0;
        Mark();
    }

    private void Mark()
    {
        HomeItem.IsCurrent = Current == Home;
        HomeItem.Desk = desks.GetValueOrDefault(Home);
        foreach (var item in Shelf)
        {
            item.IsCurrent = item.Project == Current;
            item.Desk = desks.GetValueOrDefault(item.Project);
        }
    }
}
