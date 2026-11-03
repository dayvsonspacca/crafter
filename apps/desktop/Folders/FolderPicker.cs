using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Folders;
using Crafter.Core.Projects;
using Crafter.Core.Settings;
using Crafter.Desktop.Projects;

namespace Crafter.Desktop.Folders;

public sealed partial class FolderPicker : ObservableObject, IFolderPicker
{
    private const int MostRepositories = 50;

    private readonly IPreferencesStore store;

    private readonly Bookshelf bookshelf;

    private Preferences preferences;

    private TaskCompletionSource<string?>? choosing;

    private CancellationTokenSource? finding;

    private IReadOnlyList<string> found = [];

    public FolderPicker(IPreferencesStore store, Bookshelf bookshelf)
    {
        this.store = store;
        this.bookshelf = bookshelf;
        preferences = store.Load();
        Title = string.Empty;
        Location = preferences.ProjectsRoot;
        Filter = string.Empty;
    }

    public ObservableCollection<PickerEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationLabel))]
    public partial string Location { get; set; }

    public string LocationLabel => Slashed(new FolderLabel(Location).Text);

    public string RootLabel => new FolderLabel(preferences.ProjectsRoot).Text;

    [ObservableProperty]
    public partial string Filter { get; set; }

    [ObservableProperty]
    public partial PickerEntry? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public Task<string?> Pick(string title, string start)
    {
        choosing?.TrySetResult(null);
        choosing = new TaskCompletionSource<string?>();
        preferences = store.Load();
        OnPropertyChanged(nameof(RootLabel));

        Title = title;
        Go(Directory.Exists(start) ? start : preferences.ProjectsRoot);
        IsOpen = true;
        _ = Find();

        return choosing.Task;
    }

    public void Move(int step)
    {
        List<FolderChoice> choices = [.. Entries.OfType<FolderChoice>()];
        if (choices.Count == 0)
        {
            return;
        }

        var at = Selected is FolderChoice selected ? choices.IndexOf(selected) : -1;
        Selected = at < 0
            ? choices[step > 0 ? 0 : choices.Count - 1]
            : choices[(at + step + choices.Count) % choices.Count];
    }

    public void Enter()
    {
        if (Selected is FolderChoice choice)
        {
            Go(choice.Folder);
        }
    }

    public void Up()
    {
        if (Directory.GetParent(Location) is { } parent)
        {
            Go(parent.FullName);
        }
    }

    public void Choose() => Finish(Selected is FolderChoice choice ? choice.Folder : Location);

    public void Cancel() => Finish(null);

    partial void OnFilterChanged(string value)
    {
        if (value.StartsWith('/') || value.StartsWith('~'))
        {
            var typed = FolderLabel.Expand(value);
            var folder = typed.EndsWith('/') ? typed : Path.GetDirectoryName(typed);

            if (folder is not null && Directory.Exists(folder))
            {
                Location = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
                Filter = Path.GetFileName(typed);

                return;
            }
        }
        else if (value.EndsWith('/') && Directory.Exists(Path.Combine(Location, value.TrimEnd('/'))))
        {
            Go(Path.Combine(Location, value.TrimEnd('/')));

            return;
        }

        Restock();
    }

    private void Go(string folder)
    {
        Location = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        Filter = string.Empty;
        Restock();
    }

    private void Finish(string? folder)
    {
        IsOpen = false;
        finding?.Cancel();

        var chosen = choosing;
        choosing = null;
        chosen?.TrySetResult(folder);
    }

    private async Task Find()
    {
        finding?.Cancel();
        found = [];

        if (!preferences.FindsRepositories)
        {
            return;
        }

        var mine = finding = new CancellationTokenSource();
        var finder = new RepositoryFinder(preferences.SearchDepth);
        var root = preferences.ProjectsRoot;

        try
        {
            found = await Task.Run(() => (IReadOnlyList<string>)[.. finder.Find(root, mine.Token)], mine.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Restock();
    }

    private void Restock()
    {
        var shelved = bookshelf.Projects.Select(project => project.Folder).ToHashSet(StringComparer.Ordinal);
        var wanted = Filter.Trim();

        Entries.Clear();

        List<FolderChoice> here = [.. FolderListing.Children(Location)
            .Select(folder => (Folder: folder, Hits: FuzzyMatch.Hits(Path.GetFileName(folder), wanted)))
            .Where(each => each.Hits is not null)
            .Select(each => new FolderChoice(
                each.Folder,
                Piece.Split(Path.GetFileName(each.Folder), each.Hits!),
                [],
                FolderListing.IsRepository(each.Folder),
                shelved.Contains(each.Folder)))];

        if (here.Count > 0)
        {
            Entries.Add(new PickerHeading($"Folders in {LocationLabel}"));
            here.ForEach(Entries.Add);
        }

        List<FolderChoice> repositories = [.. found
            .Select(folder => Found(folder, wanted, shelved))
            .OfType<FolderChoice>()
            .Take(MostRepositories)];

        if (repositories.Count > 0)
        {
            Entries.Add(new PickerHeading($"Repositories under {RootLabel}"));
            repositories.ForEach(Entries.Add);
        }

        Selected = Entries.OfType<FolderChoice>().FirstOrDefault();
    }

    private static FolderChoice? Found(string folder, string wanted, HashSet<string> shelved)
    {
        var name = Path.GetFileName(folder);
        var label = new FolderLabel(folder);

        if (FuzzyMatch.Hits(name, wanted) is { } inName)
        {
            return new FolderChoice(folder, Piece.Split(name, inName), Piece.Split(label.Text, []), true, shelved.Contains(folder));
        }

        if (FuzzyMatch.Hits(folder, wanted) is { } inPath)
        {
            var hits = inPath.Select(at => at - label.Shift).Where(at => at >= 0);

            return new FolderChoice(folder, Piece.Split(name, []), Piece.Split(label.Text, hits), true, shelved.Contains(folder));
        }

        return null;
    }

    private static string Slashed(string text) => text.EndsWith('/') ? text : text + "/";
}
