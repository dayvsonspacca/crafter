using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Storage.Folders;

namespace Crafter.Presentation.Projects.Namer;

// Names a folder as it joins the sidebar; once added, a project is edited on its own page.
public sealed partial class ProjectNamerViewModel : ObservableObject
{
    private TaskCompletionSource<Naming>? naming;

    public ProjectNamerViewModel()
    {
        Folder = string.Empty;
        Name = string.Empty;
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Folder { get; set; }

    [ObservableProperty]
    public partial bool IsRepository { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    public partial string Name { get; set; }

    public Task<Naming> Add(string folder, ProjectName suggested)
    {
        naming?.TrySetResult(new Dismissed());
        naming = new TaskCompletionSource<Naming>();

        Folder = ProjectName.Of(folder).Text;
        IsRepository = FolderListing.IsRepository(folder);
        Name = suggested.Text;
        IsOpen = true;

        return naming.Task;
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    public void Finish() => End(new Named(new ProjectName(Name)));

    [RelayCommand]
    public void Back() => End(new WentBack());

    public void Dismiss() => End(new Dismissed());

    private bool CanFinish() => Name.Trim().Length > 0;

    private void End(Naming outcome)
    {
        IsOpen = false;

        var asked = naming;
        naming = null;
        asked?.TrySetResult(outcome);
    }
}
