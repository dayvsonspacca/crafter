using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Folders;
using Crafter.Core.Projects;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectNamer : ObservableObject
{
    private TaskCompletionSource<Naming>? naming;

    public ProjectNamer()
    {
        Title = string.Empty;
        Folder = string.Empty;
        Name = string.Empty;
        Confirm = string.Empty;
        Retreat = string.Empty;
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsAdding { get; set; }

    [ObservableProperty]
    public partial string Folder { get; set; }

    [ObservableProperty]
    public partial bool IsRepository { get; set; }

    [ObservableProperty]
    public partial string Confirm { get; set; }

    [ObservableProperty]
    public partial string Retreat { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    public partial string Name { get; set; }

    public Task<Naming> Add(string folder, ProjectName suggested)
    {
        Folder = ProjectName.Of(folder).Text;
        IsRepository = FolderListing.IsRepository(folder);

        return Ask("Add Project", adding: true, suggested, "Add", "Back");
    }

    public Task<Naming> Rename(Project project) => Ask("Rename Project", adding: false, project.Name, "Rename", "Cancel");

    [RelayCommand(CanExecute = nameof(CanFinish))]
    public void Finish() => End(new Named(new ProjectName(Name)));

    [RelayCommand]
    public void Back() => End(IsAdding ? new WentBack() : new Dismissed());

    public void Dismiss() => End(new Dismissed());

    private bool CanFinish() => Name.Trim().Length > 0;

    private Task<Naming> Ask(string title, bool adding, ProjectName suggested, string confirm, string retreat)
    {
        naming?.TrySetResult(new Dismissed());
        naming = new TaskCompletionSource<Naming>();

        Title = title;
        IsAdding = adding;
        Name = suggested.Text;
        Confirm = confirm;
        Retreat = retreat;
        IsOpen = true;

        return naming.Task;
    }

    private void End(Naming outcome)
    {
        IsOpen = false;

        var asked = naming;
        naming = null;
        asked?.TrySetResult(outcome);
    }
}
