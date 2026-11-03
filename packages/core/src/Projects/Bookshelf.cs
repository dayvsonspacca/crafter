namespace Crafter.Core.Projects;

public sealed class Bookshelf
{
    private readonly IBookshelfStore store;

    private readonly Func<DateTimeOffset> clock;

    private readonly List<Shelved> shelf;

    public Bookshelf(IBookshelfStore store, Func<DateTimeOffset> clock)
    {
        this.store = store;
        this.clock = clock;
        shelf = [.. store.Load()];
    }

    public IReadOnlyList<Project> Projects => [.. shelf.Select(shelved => shelved.Project)];

    public Project? LastOpened => shelf.MaxBy(shelved => shelved.LastOpened)?.Project;

    public void Open(Project project)
    {
        var opened = new Shelved(project, clock());
        var place = shelf.FindIndex(shelved => shelved.Project == project);

        if (place < 0)
        {
            shelf.Add(opened);
        }
        else
        {
            shelf[place] = opened;
        }

        store.Save(shelf);
    }

    public void Remove(Project project)
    {
        if (shelf.RemoveAll(shelved => shelved.Project == project) > 0)
        {
            store.Save(shelf);
        }
    }

    public IReadOnlyList<Found> Search(string query)
    {
        var wanted = query.Trim();
        var recent = shelf.OrderByDescending(shelved => shelved.LastOpened);

        if (wanted.Length == 0)
        {
            return [.. recent.Select(shelved => new Found(shelved.Project, [], []))];
        }

        return
        [
            .. recent
                .Select(shelved => Match(shelved.Project, wanted))
                .OfType<Found>()
                .OrderBy(found => found.NameHits.Count == 0),
        ];
    }

    private static Found? Match(Project project, string wanted)
    {
        if (Hits(project.Name, wanted) is { } inName)
        {
            return new Found(project, inName, []);
        }

        return Hits(project.Folder, wanted) is { } inFolder ? new Found(project, [], inFolder) : null;
    }

    private static List<int>? Hits(string text, string wanted)
    {
        var hits = new List<int>(wanted.Length);
        var from = 0;

        foreach (var letter in wanted)
        {
            var at = text.IndexOf(letter.ToString(), from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return null;
            }

            hits.Add(at);
            from = at + 1;
        }

        return hits;
    }
}
