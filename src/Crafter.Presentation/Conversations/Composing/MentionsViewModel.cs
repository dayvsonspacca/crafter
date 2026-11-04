using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Library;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Conversations.Composing;

// The suggestions under the composer: the project's files after @ and the skills Crafter gives
// the project after /. The agent's own commands are never offered.
public sealed partial class MentionsViewModel(IProjectFiles files, Func<IReadOnlyList<Skill>>? skills = null) : ObservableObject
{
    public const int Shown = 8;

    private IReadOnlyList<string>? known;

    private CancellationTokenSource? loading;

    private int? start;

    private string query = string.Empty;

    private char mark = '@';

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

        var opening = start is null || mark != found.Mark;
        start = found.Start;
        query = found.Query;
        mark = found.Mark;
        if (opening && mark == '@')
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

        var written = (mention.IsSkill ? "/" : "@") + mention.Path + (mention.IsFolder ? string.Empty : " ");
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
        foreach (var choice in mark == '/' ? Offered(skills?.Invoke() ?? [], query) : Ranked(known ?? [], query))
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

    private static IEnumerable<MentionViewModel> Offered(IReadOnlyList<Skill> skills, string query) =>
        skills
            .Select(skill => (Skill: skill, Found: Found(skill, query)))
            .Where(each => each.Found is not null)
            .OrderBy(each => each.Found!.Value.Rank)
            .ThenBy(each => each.Skill.Id, StringComparer.Ordinal)
            .Take(Shown)
            .Select(each => new MentionViewModel(each.Skill, each.Found!.Value.Hits));

    private static (int Rank, IReadOnlyList<int> Hits)? Found(Skill skill, string query)
    {
        if (query.Length == 0)
        {
            return (0, []);
        }

        var at = skill.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            return (at == 0 ? 0 : 1, Span(at, query.Length));
        }

        if (skill.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return (2, []);
        }

        return FuzzyMatch.Hits(skill.Id, query) is { } fuzzy ? (3, fuzzy) : null;
    }

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

    // A skill is one word with no slash in it, so typing a path like /home/… offers nothing.
    private static (int Start, string Query, char Mark)? Token(string draft, int caret)
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

        if (from >= caret || draft[from] is not ('@' or '/'))
        {
            return null;
        }

        var typed = draft[(from + 1)..caret];

        return draft[from] == '/' && typed.Contains('/') ? null : (from, typed, draft[from]);
    }
}
