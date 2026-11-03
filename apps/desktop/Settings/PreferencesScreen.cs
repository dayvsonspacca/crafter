using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Settings;
using Crafter.Desktop.Folders;
using Crafter.Desktop.Setup;

namespace Crafter.Desktop.Settings;

public sealed partial class PreferencesScreen : ObservableObject
{
    private readonly IPreferencesStore store;

    public PreferencesScreen(IPreferencesStore store, IFolderPicker folders, SetupScreen setup)
    {
        this.store = store;
        AppGroups = [new ProjectsGroup(store, folders)];
        AgentGroups = [.. setup.Panels.Select(panel => new AgentGroup(panel))];
        Show(AppGroups[0]);
    }

    public Preferences Current => store.Load();

    public IReadOnlyList<PreferenceGroup> AppGroups { get; }

    public IReadOnlyList<AgentGroup> AgentGroups { get; }

    [ObservableProperty]
    public partial PreferenceGroup? Shown { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [RelayCommand]
    private void Show(PreferenceGroup group) => Shown = group;

    [RelayCommand]
    private void Leave() => IsOpen = false;

    partial void OnShownChanged(PreferenceGroup? oldValue, PreferenceGroup? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsShown = false;
        }

        if (newValue is not null)
        {
            newValue.IsShown = true;
        }
    }
}
