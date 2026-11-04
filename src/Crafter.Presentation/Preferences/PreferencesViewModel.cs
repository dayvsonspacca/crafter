using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Preferences;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Preferences;

public sealed partial class PreferencesViewModel : ObservableObject
{
    public PreferencesViewModel(PreferenceBook preferences, IFolderPicker folders, SetupViewModel setup, IThemeSwitcher themes, LibraryViewModel library)
    {
        Library = library;
        AppGroups =
        [
            new AppearanceGroupViewModel(preferences, themes),
            new ProjectsGroupViewModel(preferences, folders),
            new LibraryGroupViewModel(library),
            new ShortcutsGroupViewModel(),
        ];
        AgentGroups = [.. setup.Panels.Select(panel => new AgentGroupViewModel(panel))];
        Show(AppGroups[0]);
    }

    public LibraryViewModel Library { get; }

    public IReadOnlyList<PreferenceGroupViewModel> AppGroups { get; }

    public IReadOnlyList<AgentGroupViewModel> AgentGroups { get; }

    [ObservableProperty]
    public partial PreferenceGroupViewModel? Shown { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [RelayCommand]
    private void Show(PreferenceGroupViewModel group) => Shown = group;

    [RelayCommand]
    private void Leave() => IsOpen = false;

    partial void OnShownChanged(PreferenceGroupViewModel? oldValue, PreferenceGroupViewModel? newValue)
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
