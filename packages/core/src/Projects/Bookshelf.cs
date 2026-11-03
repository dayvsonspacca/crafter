namespace Crafter.Core.Projects;

public sealed class Bookshelf
{
    private readonly IBookshelfStore store;

    private readonly IProjectSettingsStore settings;

    private readonly Func<DateTimeOffset> clock;

    private readonly List<Shelved> shelf;

    public Bookshelf(IBookshelfStore store, IProjectSettingsStore settings, Func<DateTimeOffset> clock)
    {
        this.store = store;
        this.settings = settings;
        this.clock = clock;
        shelf = [.. store.Load().Select(shelved => shelved with { Project = Described(shelved.Project.Folder) })];
    }

    public IReadOnlyList<Project> Projects => [.. shelf.Select(shelved => shelved.Project)];

    public Project? LastOpened => shelf.MaxBy(shelved => shelved.LastOpened)?.Project;

    public ProjectName NameOf(string folder) => settings.Load(folder)?.Name ?? ProjectName.Of(folder);

    public Project Add(string folder, ProjectName name)
    {
        var icon = settings.Load(folder)?.Icon;
        var project = new Project(folder, name, icon);

        settings.Save(project.Folder, new ProjectSettings(name, project.Icon));
        Open(project);

        return project;
    }

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

    public Project Edit(Project project, ProjectName name, IconChange change)
    {
        var icon = change switch
        {
            NewIcon chosen => settings.AdoptIcon(project.Folder, chosen.File),
            NoIcon => Dropped(project.Folder),
            _ => project.Icon,
        };
        var edited = new Project(project.Folder, name, icon);
        var place = shelf.FindIndex(shelved => shelved.Project == project);

        settings.Save(edited.Folder, new ProjectSettings(name, edited.Icon));
        if (place >= 0)
        {
            shelf[place] = shelf[place] with { Project = edited };
        }

        return edited;
    }

    public void Remove(Project project)
    {
        if (shelf.RemoveAll(shelved => shelved.Project == project) > 0)
        {
            store.Save(shelf);
        }
    }

    private string? Dropped(string folder)
    {
        settings.DropIcon(folder);

        return null;
    }

    private Project Described(string folder) =>
        settings.Load(folder) is { } saved ? new Project(folder, saved.Name, saved.Icon) : new Project(folder);
}
