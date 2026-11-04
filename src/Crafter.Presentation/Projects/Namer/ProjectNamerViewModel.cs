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

    private string summarized = string.Empty;

    private AgentInstructions? instructions;

    public ProjectNamerViewModel(IFolderPicker images)
    {
        this.images = images;
        Title = string.Empty;
        Folder = string.Empty;
        Name = string.Empty;
        Confirm = string.Empty;
        Retreat = string.Empty;
        Summary = string.Empty;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial string? PreviewFile { get; set; }

    public bool HasPreview => PreviewFile is not null;

    // What the project is, read by its agent before every chat.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInherit))]
    [NotifyCanExecuteChangedFor(nameof(InheritCommand))]
    public partial string Summary { get; set; }

    // A project that brings its own CLAUDE.md or AGENTS.md can start its summary from it.
    public bool CanInherit => !IsAdding && instructions is not null && Summary.Trim().Length == 0;

    public string InheritLabel => instructions is { } found ? $"Start from {found.File}" : string.Empty;

    public string Letter => Name.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : string.Empty;

    public Task<Naming> Add(string folder, ProjectName suggested)
    {
        Folder = ProjectName.Of(folder).Text;
        IsRepository = FolderListing.IsRepository(folder);
        home = folder;
        Show(new SameIcon(), null);
        Summarize(null, null);

        return Ask("Add Project", adding: true, suggested, "Add", "Back");
    }

    public Task<Naming> Edit(Project project, string? summary = null, AgentInstructions? found = null)
    {
        home = project.Folder;
        Show(new SameIcon(), ProjectIcon.FileOf(project));
        Summarize(summary, found);

        return Ask("Edit Project", adding: false, project.Name, "Save", "Cancel");
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    public void Finish() => End(new Named(new ProjectName(Name), icon) { Summary = IsAdding || Summary.Trim() == summarized ? null : Summary.Trim() });

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

    // Fills the summary with the file's text; it is kept once the project is saved.
    [RelayCommand(CanExecute = nameof(CanInherit))]
    private void Inherit()
    {
        if (instructions is { } file)
        {
            Summary = file.Text;
        }
    }

    private void Summarize(string? summary, AgentInstructions? found)
    {
        summarized = summary?.Trim() ?? string.Empty;
        instructions = found;
        Summary = summarized;
        OnPropertyChanged(nameof(InheritLabel));
    }

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
        OnPropertyChanged(nameof(CanInherit));
        InheritCommand.NotifyCanExecuteChanged();
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
