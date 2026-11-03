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

    private readonly PreferenceBook preferences;

    private readonly Bookshelf bookshelf;

    private TaskCompletionSource<string?>? choosing;

    private CancellationTokenSource? finding;

    private IReadOnlyList<string> found = [];

    private bool images;

    public FolderPicker(PreferenceBook preferences, Bookshelf bookshelf)
    {
        this.preferences = preferences;
        this.bookshelf = bookshelf;
        Title = string.Empty;
        Location = preferences.Current.ProjectsRoot;
        Filter = string.Empty;
    }

    public ObservableCollection<PickerEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationLabel))]
    public partial string Location { get; set; }

    public string LocationLabel => Slashed(new FolderLabel(Location).Text);

    [ObservableProperty]
    public partial string Filter { get; set; }

    [ObservableProperty]
    public partial PickerEntry? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public Task<string?> Pick(string title, string start) => Open(title, start, forImages: false);

    public Task<string?> PickImage(string title, string start) => Open(title, start, forImages: true);

    public void Move(int step)
    {
        List<PickerEntry> choices = [.. Entries.Where(entry => entry is not PickerHeading)];
        if (choices.Count == 0)
        {
            return;
        }

        var at = Selected is { } selected ? choices.IndexOf(selected) : -1;
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

    public void Choose()
    {
        switch (Selected)
        {
            case ImageChoice image:
                Finish(image.File);
                break;
            case FolderChoice choice when images:
                Go(choice.Folder);
                break;
            case FolderChoice choice:
                Finish(choice.Folder);
                break;
            case null when !images:
                Finish(Location);
                break;
        }
    }

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

    private Task<string?> Open(string title, string start, bool forImages)
    {
        choosing?.TrySetResult(null);
        choosing = new TaskCompletionSource<string?>();
        images = forImages;
        found = [];

        Title = title;
        Go(Directory.Exists(start) ? start : preferences.Current.ProjectsRoot);
        IsOpen = true;
        if (!images)
        {
            _ = Find();
        }

        return choosing.Task;
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

        var mine = finding = new CancellationTokenSource();
        var finder = new RepositoryFinder();
        var root = preferences.Current.ProjectsRoot;

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
                FolderListing.IsRepository(each.Folder),
                isFound: false,
                shelved.Contains(each.Folder)))];

        if (here.Count > 0)
        {
            Entries.Add(new PickerHeading("FOLDERS"));
            here.ForEach(Entries.Add);
        }

        List<ImageChoice> pictures = images ? [.. Pictures(wanted)] : [];

        if (pictures.Count > 0)
        {
            Entries.Add(new PickerHeading("IMAGES"));
            pictures.ForEach(Entries.Add);
        }

        List<FolderChoice> repositories = [.. found
            .Select(folder => Found(folder, wanted, shelved))
            .OfType<FolderChoice>()
            .Take(MostRepositories)];

        if (repositories.Count > 0)
        {
            Entries.Add(new PickerHeading("REPOSITORIES"));
            repositories.ForEach(Entries.Add);
        }

        Selected = (PickerEntry?)pictures.FirstOrDefault() ?? Entries.OfType<FolderChoice>().FirstOrDefault();
    }

    private IEnumerable<ImageChoice> Pictures(string wanted)
    {
        IEnumerable<string> files;
        try
        {
            files = [.. Directory.EnumerateFiles(Location).Where(file => ImageChoice.Formats.Contains(Path.GetExtension(file)))];
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith('.') && FuzzyMatch.Hits(name, wanted) is { } hits)
            {
                yield return new ImageChoice(file, Piece.Split(name, hits));
            }
        }
    }

    private static FolderChoice? Found(string folder, string wanted, HashSet<string> shelved)
    {
        var name = Path.GetFileName(folder);

        if (FuzzyMatch.Hits(name, wanted) is { } inName)
        {
            return new FolderChoice(folder, Piece.Split(name, inName), isRepository: true, isFound: true, shelved.Contains(folder));
        }

        return FuzzyMatch.Hits(folder, wanted) is not null
            ? new FolderChoice(folder, Piece.Split(name, []), isRepository: true, isFound: true, shelved.Contains(folder))
            : null;
    }

    private static string Slashed(string text) => text.EndsWith('/') ? text : text + "/";
}
