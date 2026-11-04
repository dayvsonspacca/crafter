using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Preferences;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Preferences;

public sealed partial class ProjectsGroupViewModel : PreferenceGroupViewModel
{
    private readonly PreferenceBook preferences;

    private readonly IFolderPicker folders;

    private readonly bool ready;

    public ProjectsGroupViewModel(PreferenceBook preferences, IFolderPicker folders)
        : base("Projects")
    {
        this.preferences = preferences;
        this.folders = folders;
        Root = new FolderLabel(preferences.Current.ProjectsRoot).Text;
        ready = true;
    }

    [ObservableProperty]
    public partial string Root { get; set; }

    [ObservableProperty]
    public partial bool IsRootMissing { get; set; }

    [RelayCommand]
    private async Task Browse()
    {
        if (await folders.Pick("Choose the projects root", preferences.Current.ProjectsRoot) is { } folder)
        {
            Root = new FolderLabel(folder).Text;
        }
    }

    partial void OnRootChanged(string value)
    {
        var folder = FolderLabel.Expand(value.Trim());

        IsRootMissing = folder.Length == 0 || !Directory.Exists(folder);
        if (ready && !IsRootMissing)
        {
            preferences.MoveProjectsRoot(folder);
        }
    }
}
