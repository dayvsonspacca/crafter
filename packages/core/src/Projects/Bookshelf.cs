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
        shelf = [.. store.Load().Select(shelved => shelved with { Project = new Project(shelved.Project.Folder, NameOf(shelved.Project.Folder)) })];
    }

    public IReadOnlyList<Project> Projects => [.. shelf.Select(shelved => shelved.Project)];

    public Project? LastOpened => shelf.MaxBy(shelved => shelved.LastOpened)?.Project;

    public ProjectName NameOf(string folder) => settings.Load(folder)?.Name ?? ProjectName.Of(folder);

    public Project Add(string folder, ProjectName name)
    {
        var project = new Project(folder, name);

        settings.Save(project.Folder, new ProjectSettings(name));
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

    public Project Rename(Project project, ProjectName name)
    {
        var renamed = new Project(project.Folder, name);
        var place = shelf.FindIndex(shelved => shelved.Project == project);

        settings.Save(renamed.Folder, new ProjectSettings(name));
        if (place >= 0)
        {
            shelf[place] = shelf[place] with { Project = renamed };
        }

        return renamed;
    }

    public void Remove(Project project)
    {
        if (shelf.RemoveAll(shelved => shelved.Project == project) > 0)
        {
            store.Save(shelf);
        }
    }
}
