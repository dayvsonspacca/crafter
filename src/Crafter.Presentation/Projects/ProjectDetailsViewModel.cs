using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Projects;

// A project's name, icon and summary, edited on its page and kept only once saved.
public sealed partial class ProjectDetailsViewModel : ObservableObject
{
    private readonly IProjectEditor editor;

    private readonly IFolderPicker images;

    private Project project;

    private IconChange icon = new SameIcon();

    private string summarized = string.Empty;

    private AgentInstructions? instructions;

    public ProjectDetailsViewModel(Project project, IProjectEditor editor, IFolderPicker images)
    {
        this.project = project;
        this.editor = editor;
        this.images = images;
        Name = string.Empty;
        Summary = string.Empty;
        Reload();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Letter), nameof(IsChanged))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(DiscardCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial string? PreviewFile { get; set; }

    public bool HasPreview => PreviewFile is not null;

    public string Letter => Name.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : string.Empty;

    // What the project is, read by its agent before every chat.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInherit), nameof(IsChanged))]
    [NotifyCanExecuteChangedFor(nameof(InheritCommand), nameof(SaveCommand), nameof(DiscardCommand))]
    public partial string Summary { get; set; }

    // A project that brings its agents' own instructions file can start its summary from it.
    public bool CanInherit => instructions is not null && Summary.Trim().Length == 0;

    public string InheritLabel => instructions is { } found ? $"Start from {found.File}" : string.Empty;

    public bool IsChanged => Name.Trim() != project.Name.Text || icon is not SameIcon || Summary.Trim() != summarized;

    // Shows the project as it is saved, dropping whatever was changed and not saved.
    public void Reload()
    {
        Name = project.Name.Text;
        Show(new SameIcon(), ProjectIcon.FileOf(project));
        summarized = editor.SummaryOf(project)?.Trim() ?? string.Empty;
        instructions = editor.InstructionsIn(project);
        Summary = summarized;
        OnPropertyChanged(nameof(InheritLabel));
        OnPropertyChanged(nameof(CanInherit));
        InheritCommand.NotifyCanExecuteChanged();
    }

    // The project was renamed or given a summary from elsewhere; an edit in progress stays.
    public void Describe(Project described)
    {
        var untouched = !IsChanged;
        project = described;
        if (untouched)
        {
            Reload();
        }
        else
        {
            summarized = editor.SummaryOf(project)?.Trim() ?? string.Empty;
            Changed();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var summary = Summary.Trim();
        project = editor.Save(project, new ProjectName(Name), icon, summary == summarized ? null : summary);
        Reload();
    }

    private bool CanSave() => IsChanged && Name.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void Discard() => Reload();

    [RelayCommand]
    private async Task ChooseIcon()
    {
        if (await images.PickImage("Choose an icon", project.Folder) is { } file)
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

    private void Show(IconChange change, string? preview)
    {
        icon = change;
        PreviewFile = preview;
        Changed();
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(IsChanged));
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }
}
