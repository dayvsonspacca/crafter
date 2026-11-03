using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Core.Settings;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.Folders;
using Crafter.Desktop.Projects;
using Crafter.Desktop.Settings;
using Crafter.Desktop.Setup;
using Crafter.Desktop.Themes;

namespace Crafter.Desktop;

public sealed partial class WorkbenchScreen : ObservableObject, IShelfKeeper
{
    private readonly Bookshelf bookshelf;

    private readonly IChatArchive archive;

    private readonly Crew crew;

    private readonly Func<string, Task> copy;

    private readonly Dictionary<Project, ProjectDesk> desks = [];

    public WorkbenchScreen(
        Bookshelf bookshelf,
        IPreferencesStore preferences,
        IChatArchive archive,
        Crew crew,
        Func<string, Task> copy,
        Action<Theme> apply)
    {
        this.bookshelf = bookshelf;
        this.archive = archive;
        this.crew = crew;
        this.copy = copy;
        Picker = new FolderPicker(preferences, bookshelf);
        Namer = new ProjectNamer(Picker);
        Setup = new SetupScreen(crew, preferences, copy);
        Preferences = new PreferencesScreen(preferences, Picker, Setup, apply);
        Preferences.PropertyChanged += Follow;
        Setup.PropertyChanged += Follow;
        if (!Setup.IsPicking)
        {
            _ = Setup.Check();
        }

        IsSidebarVisible = true;
        Restock();

        if (bookshelf.LastOpened is { } last && Directory.Exists(last.Folder))
        {
            Open(last);
        }
    }

    public FolderPicker Picker { get; }

    public ProjectNamer Namer { get; }

    public PreferencesScreen Preferences { get; }

    public SetupScreen Setup { get; }

    public ObservableCollection<ShelfItem> Shelf { get; } = [];

    public bool IsShowingPreferences => Preferences.IsOpen;

    public bool ShowsConversation => Desk is not null;

    public bool ShowsHint => Desk is null;

    public string Hint => HasProjects ? "Pick a project to start crafting." : "Add a project to start crafting.";

    public bool ShowsSetup => Setup.NeedsSetup;

    public bool IsCrafting => desks.Values.Any(desk => desk.IsCrafting);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial bool HasProjects { get; set; }

    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; }

    public Project? Current
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Heading));
        }
    }

    public string? Heading => ShowsSetup ? "Setup" : Current?.Name.Text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsConversation), nameof(ShowsHint))]
    public partial ProjectDesk? Desk { get; set; }

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

        bookshelf.Open(project);
        if (!desks.TryGetValue(project, out var desk))
        {
            desk = new ProjectDesk(project, archive, crew, Edit);
            desks[project] = desk;
        }

        Current = project;
        Desk = desk;
        desk.IsShown = true;
        desk.ShowOverview();
        Restock();
    }

    [RelayCommand]
    public void Close()
    {
        if (Desk is { } desk)
        {
            desk.Leave();
            desk.IsShown = false;
            desks.Remove(desk.Project);
        }

        Desk = null;
        Current = null;
        Mark();
    }

    [RelayCommand]
    public void Remove(Project project)
    {
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

    public Task CopyPath(Project project) => copy(project.Folder);

    public async Task Edit(Project project)
    {
        if (await Namer.Edit(project) is not Named named)
        {
            return;
        }

        var edited = bookshelf.Edit(project, named.Name, named.Icon);
        if (desks.TryGetValue(edited, out var desk))
        {
            desk.Describe(edited);
        }

        if (edited == Current)
        {
            Current = edited;
        }

        Restock();
    }

    [RelayCommand]
    private async Task Add()
    {
        var start = Preferences.Current.ProjectsRoot;

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
    private Task EditCurrent() => Current is { } project ? Edit(project) : Task.CompletedTask;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    private void TogglePreferences() => Preferences.IsOpen = !Preferences.IsOpen;

    [RelayCommand]
    private void NewChat() => Desk?.NewChat();

    [RelayCommand]
    private void CloseChat() => Desk?.CloseSelected();

    [RelayCommand]
    private void NextChat() => Desk?.Next(1);

    [RelayCommand]
    private void PreviousChat() => Desk?.Next(-1);

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName is nameof(PreferencesScreen.IsOpen) or nameof(SetupScreen.NeedsSetup))
        {
            OnPropertyChanged(nameof(IsShowingPreferences));
            OnPropertyChanged(nameof(ShowsConversation));
            OnPropertyChanged(nameof(ShowsHint));
            OnPropertyChanged(nameof(ShowsSetup));
            OnPropertyChanged(nameof(Heading));
        }
    }

    private void Restock()
    {
        var projects = bookshelf.Projects;

        Shelf.Clear();
        for (var place = 0; place < projects.Count; place++)
        {
            Shelf.Add(new ShelfItem(projects[place], ProjectShortcuts.Slot(place), this));
        }

        HasProjects = Shelf.Count > 0;
        Mark();
    }

    private void Mark()
    {
        foreach (var item in Shelf)
        {
            item.IsCurrent = item.Project == Current;
            item.Desk = desks.GetValueOrDefault(item.Project);
        }
    }
}
