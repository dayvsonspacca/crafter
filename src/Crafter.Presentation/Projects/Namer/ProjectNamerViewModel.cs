using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Presentation.Folders;
using Crafter.Storage.Folders;

namespace Crafter.Presentation.Projects.Namer;

public sealed partial class ProjectNamerViewModel : ObservableObject
{
    private readonly IFolderPicker images;

    private TaskCompletionSource<Naming>? naming;

    private string home = string.Empty;

    private IconChange icon = new SameIcon();

    public ProjectNamerViewModel(IFolderPicker images)
    {
        this.images = images;
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

    // The project's agent may see your projects, rename them and write to the skills library.
    [ObservableProperty]
    public partial bool ManagesCrafter { get; set; }

    [ObservableProperty]
    public partial string Confirm { get; set; }

    [ObservableProperty]
    public partial string Retreat { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial string? PreviewFile { get; set; }

    public bool HasPreview => PreviewFile is not null;

    public string Letter => Name.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : string.Empty;

    public Task<Naming> Add(string folder, ProjectName suggested)
    {
        Folder = ProjectName.Of(folder).Text;
        IsRepository = FolderListing.IsRepository(folder);
        home = folder;
        Show(new SameIcon(), null);

        return Ask("Add Project", adding: true, suggested, "Add", "Back");
    }

    public Task<Naming> Edit(Project project, bool managesCrafter = false)
    {
        home = project.Folder;
        ManagesCrafter = managesCrafter;
        Show(new SameIcon(), ProjectIcon.FileOf(project));

        return Ask("Edit Project", adding: false, project.Name, "Save", "Cancel");
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    public void Finish() => End(new Named(new ProjectName(Name), icon));

    [RelayCommand]
    public void Back() => End(IsAdding ? new WentBack() : new Dismissed());

    public void Dismiss() => End(new Dismissed());

    private bool CanFinish() => Name.Trim().Length > 0;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Letter));

    [RelayCommand]
    private async Task ChooseIcon()
    {
        if (await images.PickImage("Choose an icon", home) is { } file)
        {
            Show(new NewIcon(file), file);
        }
    }

    [RelayCommand]
    private void RemoveIcon() => Show(new NoIcon(), null);

    private void Show(IconChange change, string? preview)
    {
        icon = change;
        PreviewFile = preview;
    }

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
