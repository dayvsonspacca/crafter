using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Conversations.Composing;

public sealed partial class MentionsViewModel(IProjectFiles files) : ObservableObject
{
    public const int Shown = 8;

    private IReadOnlyList<string>? known;

    private CancellationTokenSource? loading;

    private int? start;

    private string query = string.Empty;

    public ObservableCollection<MentionViewModel> Choices { get; } = [];

    [ObservableProperty]
    public partial MentionViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    partial void OnSelectedChanged(MentionViewModel? oldValue, MentionViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

    public void Look(string draft, int caret)
    {
        if (Token(draft, caret) is not { } found)
        {
            Close();

            return;
        }

        var opening = start is null;
        start = found.Start;
        query = found.Query;
        if (opening)
        {
            _ = Load();
        }

        Restock();
    }

    public void Move(int step)
    {
        if (Choices.Count == 0)
        {
            return;
        }

        var at = Selected is { } selected ? Choices.IndexOf(selected) : -1;
        Selected = Choices[(at + step + Choices.Count) % Choices.Count];
    }

    public (string Draft, int Caret)? Accept(string draft, MentionViewModel? chosen = null)
    {
        if ((chosen ?? Selected) is not { } mention || start is not { } from || from >= draft.Length)
        {
            return null;
        }

        var end = from;
        while (end < draft.Length && !char.IsWhiteSpace(draft[end]))
        {
            end++;
        }

        var written = "@" + mention.Path + (mention.IsFolder ? string.Empty : " ");
        if (!mention.IsFolder)
        {
            Close();
        }

        return (draft[..from] + written + draft[end..], from + written.Length);
    }

    public void Close()
    {
        start = null;
        loading?.Cancel();
        IsOpen = false;
        Selected = null;
        Choices.Clear();
    }

    private async Task Load()
    {
        loading?.Cancel();
        var mine = loading = new CancellationTokenSource();

        try
        {
            known = await files.List(mine.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!mine.IsCancellationRequested && start is not null)
        {
            Restock();
        }
    }

    private void Restock()
    {
        Choices.Clear();
        foreach (var choice in Ranked(known ?? [], query))
        {
            Choices.Add(choice);
        }

        Selected = Choices.FirstOrDefault();
        IsOpen = Choices.Count > 0;
    }

    private static IEnumerable<MentionViewModel> Ranked(IReadOnlyList<string> paths, string query) =>
        paths
            .Select(path => (Path: path, Found: Found(path, query)))
            .Where(each => each.Found is not null)
            .OrderBy(each => each.Found!.Value.Rank)
            .ThenBy(each => each.Path.Length)
            .ThenBy(each => each.Path, StringComparer.Ordinal)
            .Take(Shown)
            .Select(each => new MentionViewModel(each.Path, each.Found!.Value.Hits));

    private static (int Rank, IReadOnlyList<int> Hits)? Found(string path, string query)
    {
        if (query.Length == 0)
        {
            return (path.Count(letter => letter == '/'), []);
        }

        var name = Name(path);
        var at = path.Length - name.Length - (path.EndsWith('/') ? 1 : 0);
        var inName = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (inName >= 0)
        {
            return (inName == 0 ? 0 : 1, Span(at + inName, query.Length));
        }

        var inPath = path.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (inPath >= 0)
        {
            return (2, Span(inPath, query.Length));
        }

        if (FuzzyMatch.Hits(name, query) is { } fuzzyName)
        {
            return (3, [.. fuzzyName.Select(hit => at + hit)]);
        }

        return FuzzyMatch.Hits(path, query) is { } fuzzyPath ? (4, fuzzyPath) : null;
    }

    private static string Name(string path)
    {
        var trimmed = path.TrimEnd('/');

        return trimmed[(trimmed.LastIndexOf('/') + 1)..];
    }

    private static IReadOnlyList<int> Span(int from, int length) => [.. Enumerable.Range(from, length)];

    private static (int Start, string Query)? Token(string draft, int caret)
    {
        if (caret < 1 || caret > draft.Length)
        {
            return null;
        }

        var from = caret;
        while (from > 0 && !char.IsWhiteSpace(draft[from - 1]))
        {
            from--;
        }

        return from < caret && draft[from] == '@' ?(from, draft[(from + 1)..caret]) : null;
    }
}
