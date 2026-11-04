using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Keyboard;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Projects.Namer;

namespace Crafter.Presentation;

public sealed partial class WorkbenchViewModel : ObservableObject, IShelfKeeper
{
    private readonly Bookshelf bookshelf;

    private readonly PreferenceBook preferences;

    private readonly DeskFactory deskFactory;

    private readonly IClipboard clipboard;

    private readonly Dictionary<Project, ProjectDeskViewModel> desks = [];

    public WorkbenchViewModel(
        Bookshelf bookshelf,
        PreferenceBook preferences,
        FolderPickerViewModel picker,
        ProjectNamerViewModel namer,
        SetupViewModel setup,
        PreferencesViewModel preferencesScreen,
        DeskFactory deskFactory,
        IClipboard clipboard)
    {
        this.bookshelf = bookshelf;
        this.preferences = preferences;
        this.deskFactory = deskFactory;
        this.clipboard = clipboard;
        Picker = picker;
        Namer = namer;
        Setup = setup;
        Preferences = preferencesScreen;
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

    public FolderPickerViewModel Picker { get; }

    public ProjectNamerViewModel Namer { get; }

    public PreferencesViewModel Preferences { get; }

    public SetupViewModel Setup { get; }

    public ObservableCollection<ShelfItemViewModel> Shelf { get; } = [];

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
    public partial ProjectDeskViewModel? Desk { get; set; }

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
            desk = deskFactory.Make(project, Edit);
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

    public Task CopyPath(Project project) => clipboard.Copy(project.Folder);

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
        if (changed.PropertyName is nameof(PreferencesViewModel.IsOpen) or nameof(SetupViewModel.NeedsSetup))
        {
            NotifyDerived();
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(IsShowingPreferences));
        OnPropertyChanged(nameof(ShowsConversation));
        OnPropertyChanged(nameof(ShowsHint));
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
        foreach (var item in Shelf)
        {
            item.IsCurrent = item.Project == Current;
            item.Desk = desks.GetValueOrDefault(item.Project);
        }
    }
}
