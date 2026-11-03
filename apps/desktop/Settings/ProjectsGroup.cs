using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Settings;
using Crafter.Desktop.Folders;
using Crafter.Desktop.Projects;

namespace Crafter.Desktop.Settings;

public sealed partial class ProjectsGroup : PreferenceGroup
{
    private readonly IPreferencesStore store;

    private readonly IFolderPicker folders;

    private readonly bool ready;

    public ProjectsGroup(IPreferencesStore store, IFolderPicker folders)
        : base("Projects")
    {
        this.store = store;
        this.folders = folders;
        Root = new FolderLabel(store.Load().ProjectsRoot).Text;
        ready = true;
    }

    [ObservableProperty]
    public partial string Root { get; set; }

    [ObservableProperty]
    public partial bool IsRootMissing { get; set; }

    [RelayCommand]
    private async Task Browse()
    {
        if (await folders.Pick("Choose the projects root", store.Load().ProjectsRoot) is { } folder)
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
            var current = store.Load();
            var changed = current with { ProjectsRoot = folder };
            if (changed != current)
            {
                store.Save(changed);
            }
        }
    }
}
