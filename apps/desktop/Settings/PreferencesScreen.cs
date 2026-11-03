using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Settings;
using Crafter.Desktop.Folders;
using Crafter.Desktop.Setup;
using Crafter.Desktop.Themes;

namespace Crafter.Desktop.Settings;

public sealed partial class PreferencesScreen : ObservableObject
{
    public PreferencesScreen(PreferenceBook preferences, IFolderPicker folders, SetupScreen setup, IThemeSwitcher themes)
    {
        AppGroups = [new AppearanceGroup(preferences, themes), new ProjectsGroup(preferences, folders)];
        AgentGroups = [.. setup.Panels.Select(panel => new AgentGroup(panel))];
        Show(AppGroups[0]);
    }

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
