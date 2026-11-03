using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.Projects;

namespace Crafter.Desktop;

public sealed partial class WorkbenchScreen : ObservableObject
{
    private readonly Bookshelf bookshelf;

    private readonly Func<Project, ConversationScreen> converse;

    private readonly IFolderPicker folders;

    public WorkbenchScreen(Bookshelf bookshelf, Func<Project, ConversationScreen> converse, IFolderPicker folders)
    {
        this.bookshelf = bookshelf;
        this.converse = converse;
        this.folders = folders;
        Palette = new ProjectPalette(bookshelf, Open);
        Projects = [.. bookshelf.Projects];

        if (bookshelf.LastOpened is { } last && Directory.Exists(last.Folder))
        {
            Open(last);
        }
    }

    public ProjectPalette Palette { get; }

    public ObservableCollection<Project> Projects { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFolder))]
    public partial Project? Current { get; set; }

    public string? CurrentFolder => Current is null ? null : new FolderLabel(Current.Folder).Text;

    [ObservableProperty]
    public partial ConversationScreen? Conversation { get; set; }

    public void OpenSlot(int place)
    {
        if (place < Projects.Count)
        {
            Open(Projects[place]);
        }
    }

    [RelayCommand]
    private void Open(Project project)
    {
        if (project == Current)
        {
            return;
        }

        Conversation?.Leave();
        bookshelf.Open(project);
        Current = project;
        Conversation = converse(project);
        Restock();
    }

    [RelayCommand]
    private async Task Add()
    {
        if (await folders.Pick() is { } folder)
        {
            Open(new Project(folder));
        }
    }

    [RelayCommand]
    private void Search() => Palette.Show();

    [RelayCommand]
    private void Close()
    {
        Conversation?.Leave();
        Conversation = null;
        Current = null;
    }

    [RelayCommand]
    private void RemoveCurrent()
    {
        if (Current is not { } project)
        {
            return;
        }

        Close();
        bookshelf.Remove(project);
        Restock();
    }

    private void Restock()
    {
        Projects.Clear();
        foreach (var project in bookshelf.Projects)
        {
            Projects.Add(project);
        }
    }
}
