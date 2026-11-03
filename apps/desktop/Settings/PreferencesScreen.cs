using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Settings;
using Crafter.Desktop.Folders;
using Crafter.Desktop.Projects;
using Crafter.Desktop.Setup;

namespace Crafter.Desktop.Settings;

public sealed partial class PreferencesScreen : ObservableObject
{
    private readonly IPreferencesStore store;

    private readonly IFolderPicker folders;

    private readonly bool ready;

    public PreferencesScreen(IPreferencesStore store, IFolderPicker folders, SetupScreen setup)
    {
        Agents = setup.Cards;
        this.store = store;
        this.folders = folders;

        var current = store.Load();
        Root = new FolderLabel(current.ProjectsRoot).Text;
        FindsRepositories = current.FindsRepositories;
        SearchDepth = current.SearchDepth;
        ready = true;
    }

    public Preferences Current => store.Load();

    public IReadOnlyList<AgentCard> Agents { get; }

    public int ShallowestSearch => Preferences.ShallowestSearch;

    public int DeepestSearch => Preferences.DeepestSearch;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Root { get; set; }

    [ObservableProperty]
    public partial bool IsRootMissing { get; set; }

    [ObservableProperty]
    public partial bool FindsRepositories { get; set; }

    [ObservableProperty]
    public partial decimal? SearchDepth { get; set; }

    [RelayCommand]
    private async Task Browse()
    {
        if (await folders.Pick("Choose the projects root", Current.ProjectsRoot) is { } folder)
        {
            Root = new FolderLabel(folder).Text;
        }
    }

    [RelayCommand]
    private void Leave() => IsOpen = false;

    partial void OnRootChanged(string value)
    {
        var folder = FolderLabel.Expand(value.Trim());

        IsRootMissing = folder.Length == 0 || !Directory.Exists(folder);
        if (!IsRootMissing)
        {
            Save(current => current with { ProjectsRoot = folder });
        }
    }

    partial void OnFindsRepositoriesChanged(bool value) => Save(current => current with { FindsRepositories = value });

    partial void OnSearchDepthChanged(decimal? value)
    {
        if (value is { } depth)
        {
            Save(current => current with { SearchDepth = (int)depth });
        }
    }

    private void Save(Func<Preferences, Preferences> change)
    {
        if (!ready)
        {
            return;
        }

        var current = store.Load();
        var changed = change(current);
        if (changed != current)
        {
            store.Save(changed);
        }
    }
}
