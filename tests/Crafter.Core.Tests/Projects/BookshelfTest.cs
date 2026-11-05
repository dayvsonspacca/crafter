using Crafter.Core.Projects;
using Crafter.Testing;

namespace Crafter.Core.Tests.Projects;

public sealed class BookshelfTest
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly Project Crafter = new("/home/hero/Projetos/me/desktop/crafter");

    private static readonly Project Beacon = new("/home/hero/Projetos/me/beacon");

    private static readonly Project Reborn = new("/home/hero/Projetos/me/reborn");

    [Fact]
    public void StartsWithWhatWasStored()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), new SettingsInMemory(), () => Morning);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
    }

    [Fact]
    public void ShelvesANewProjectAtTheEnd()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning));
        var bookshelf = new Bookshelf(store, new SettingsInMemory(), () => Morning.AddHours(1));

        bookshelf.Open(Beacon);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
        Assert.Equal([new Shelved(Crafter, Morning), new Shelved(Beacon, Morning.AddHours(1))], store.Saved);
    }

    [Fact]
    public void KeepsTheProjectInPlaceWhenReopened()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning));
        var bookshelf = new Bookshelf(store, new SettingsInMemory(), () => Morning.AddHours(2));

        bookshelf.Open(Crafter);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
        Assert.Equal(new Shelved(Crafter, Morning.AddHours(2)), store.Saved[0]);
    }

    [Fact]
    public void RemovesAProject()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning));
        var bookshelf = new Bookshelf(store, new SettingsInMemory(), () => Morning);

        bookshelf.Remove(Crafter);

        Assert.Equal([Beacon], bookshelf.Projects);
        Assert.Equal([new Shelved(Beacon, Morning)], store.Saved);
    }

    [Fact]
    public void LeavesTheStoreAloneWhenRemovingAnUnknownProject()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning));

        new Bookshelf(store, new SettingsInMemory(), () => Morning).Remove(Beacon);

        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void NamesStoredProjectsFromTheirSettings()
    {
        var settings = new SettingsInMemory();
        settings.Save(Crafter.Folder, new ProjectSettings(new ProjectName("Crafter · desktop")));

        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), settings, () => Morning);

        Assert.Equal(["Crafter · desktop", "beacon"], bookshelf.Projects.Select(project => project.Name.Text));
    }

    [Fact]
    public void SuggestsTheFolderNameWithoutSettings()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        Assert.Equal(new ProjectName("reborn"), bookshelf.NameOf(Reborn.Folder));
    }

    [Fact]
    public void AddsANamedProjectAndKeepsTheNameInItsSettings()
    {
        var store = new ShelfInMemory();
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(store, settings, () => Morning);

        var added = bookshelf.Add(Beacon.Folder, new ProjectName("Beacon"));

        Assert.Equal("Beacon", added.Name.Text);
        Assert.Equal([added], bookshelf.Projects);
        Assert.Equal(new ProjectName("Beacon"), settings.Load(Beacon.Folder)?.Name);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void EditsAProjectInPlace()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), settings, () => Morning);

        var edited = bookshelf.Edit(Crafter, new ProjectName("Crafter"), new NewIcon("/home/hero/Imagens/crafter.png"));

        Assert.Equal("Crafter", edited.Name.Text);
        Assert.Equal(".crafter/icon.png", edited.Icon);
        Assert.Equal(["/home/hero/Imagens/crafter.png"], settings.Adopted);
        Assert.Equal(["Crafter", "beacon"], bookshelf.Projects.Select(project => project.Name.Text));
        Assert.Equal(new ProjectSettings(new ProjectName("Crafter"), ".crafter/icon.png"), settings.Load(Crafter.Folder));
    }

    [Fact]
    public void KeepsTheIconWhenItStaysTheSame()
    {
        var settings = new SettingsInMemory();
        var iconic = new Project(Crafter.Folder, new ProjectName("crafter"), ".crafter/icon.png");
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(iconic, Morning)), settings, () => Morning);

        var edited = bookshelf.Edit(iconic, new ProjectName("Crafter"), new SameIcon());

        Assert.Equal(".crafter/icon.png", edited.Icon);
        Assert.Empty(settings.Adopted);
        Assert.Empty(settings.Dropped);
    }

    [Fact]
    public void DropsTheIcon()
    {
        var settings = new SettingsInMemory();
        var iconic = new Project(Crafter.Folder, new ProjectName("crafter"), ".crafter/icon.png");

        var edited = new Bookshelf(new ShelfInMemory(), settings, () => Morning).Edit(iconic, iconic.Name, new NoIcon());

        Assert.Null(edited.Icon);
        Assert.Equal([Crafter.Folder], settings.Dropped);
    }

    [Fact]
    public void KeepsTheIconWhenAddingAFolderThatHasOne()
    {
        var settings = new SettingsInMemory();
        settings.Save(Beacon.Folder, new ProjectSettings(new ProjectName("beacon"), "logo.png"));

        var added = new Bookshelf(new ShelfInMemory(), settings, () => Morning).Add(Beacon.Folder, new ProjectName("Beacon"));

        Assert.Equal("logo.png", added.Icon);
    }

    [Fact]
    public void ReadsTheIconOfStoredProjects()
    {
        var settings = new SettingsInMemory();
        settings.Save(Crafter.Folder, new ProjectSettings(new ProjectName("Crafter"), "logo.png"));

        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), settings, () => Morning);

        Assert.Equal("logo.png", Assert.Single(bookshelf.Projects).Icon);
    }

    [Fact]
    public void EditsAProjectOffTheShelfWithoutShelvingIt()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        bookshelf.Edit(Reborn, new ProjectName("Reborn"), new SameIcon());

        Assert.Empty(bookshelf.Projects);
        Assert.Equal(new ProjectName("Reborn"), bookshelf.NameOf(Reborn.Folder));
    }

    [Fact]
    public void HoldsAndDropsAxiomsInOrderInTheProjectsOwnSettings()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), settings, () => Morning.AddHours(1));

        Assert.True(bookshelf.Hold(Crafter, " Never add comments "));
        Assert.True(bookshelf.Hold(Crafter, "Always write tests"));
        Assert.False(bookshelf.Hold(Crafter, "Never add comments"));
        Assert.False(bookshelf.Hold(Crafter, " "));
        bookshelf.Open(Crafter);

        Assert.Equal(["Never add comments", "Always write tests"], bookshelf.AxiomsOf(Crafter));
        Assert.Empty(bookshelf.AxiomsOf(Beacon));
        Assert.Equal(["Never add comments", "Always write tests"], settings.Load(Crafter.Folder)!.Axioms);

        Assert.True(bookshelf.Drop(Crafter, "Never add comments"));
        Assert.False(bookshelf.Drop(Crafter, "Never add comments"));

        Assert.Equal(["Always write tests"], settings.Load(Crafter.Folder)!.Axioms);
    }

    [Fact]
    public void KeepsTheAxiomsThroughARenameAndANewIcon()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), settings, () => Morning);
        bookshelf.Hold(Crafter, "Never add comments");

        var renamed = bookshelf.Edit(Crafter, new ProjectName("Crafter · desktop"), new NewIcon("/tmp/logo.png"));

        Assert.Equal(["Never add comments"], bookshelf.AxiomsOf(renamed));
        Assert.Equal(new ProjectName("Crafter · desktop"), settings.Load(Crafter.Folder)!.Name);
    }

    [Fact]
    public void AddingAProjectKeepsTheAxiomsItsFolderBrings()
    {
        var settings = new SettingsInMemory();
        settings.Save(Beacon.Folder, new ProjectSettings(new ProjectName("Beacon")) { Axioms = ["Always write tests"] });
        var bookshelf = new Bookshelf(new ShelfInMemory(), settings, () => Morning);

        var added = bookshelf.Add(Beacon.Folder, new ProjectName("Beacon · web"));

        Assert.Equal(["Always write tests"], bookshelf.AxiomsOf(added));
    }

    [Fact]
    public void KeepsAndDropsTheSummaryInTheProjectsOwnFolder()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), settings, () => Morning);

        Assert.True(bookshelf.Summarize(Crafter, " Crafter is my IDE. "));
        Assert.False(bookshelf.Summarize(Crafter, "Crafter is my IDE."));

        Assert.Equal("Crafter is my IDE.", bookshelf.SummaryOf(Crafter));
        Assert.Equal("Crafter is my IDE.", settings.LoadSummary(Crafter.Folder));

        Assert.True(bookshelf.Summarize(Crafter, " "));
        Assert.Null(bookshelf.SummaryOf(Crafter));
    }

    [Fact]
    public void SummarizesNoProjectOffTheShelf()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        Assert.False(bookshelf.Summarize(Crafter, "Crafter is my IDE."));
        Assert.Null(bookshelf.SummaryOf(Crafter));
    }

    [Fact]
    public void HoldsNoAxiomForAProjectOffTheShelf()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        Assert.False(bookshelf.Hold(Crafter, "Never add comments"));
        Assert.Empty(bookshelf.AxiomsOf(Crafter));
    }

    [Fact]
    public void JotsTodosNumberedInTurnInTheProjectsOwnFolder()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), settings, () => Morning);

        Assert.Equal(new Todo(1, "Draw the panel"), bookshelf.Jot(Crafter, " Draw the panel "));
        Assert.Equal(new Todo(2, "Cite a todo"), bookshelf.Jot(Crafter, "Cite a todo"));
        Assert.Null(bookshelf.Jot(Crafter, " "));

        Assert.Equal([new Todo(1, "Draw the panel"), new Todo(2, "Cite a todo")], bookshelf.TodosOf(Crafter));
        Assert.Equal(bookshelf.TodosOf(Crafter), settings.LoadTodos(Crafter.Folder));
        Assert.Empty(bookshelf.TodosOf(Beacon));
    }

    [Fact]
    public void TicksAndScrapsATodoByItsNumber()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), new SettingsInMemory(), () => Morning);
        bookshelf.Jot(Crafter, "Draw the panel");
        bookshelf.Jot(Crafter, "Cite a todo");

        Assert.True(bookshelf.Tick(Crafter, 1, done: true));
        Assert.False(bookshelf.Tick(Crafter, 1, done: true));
        Assert.False(bookshelf.Tick(Crafter, 7, done: true));
        Assert.Equal([new Todo(1, "Draw the panel", IsDone: true), new Todo(2, "Cite a todo")], bookshelf.TodosOf(Crafter));

        Assert.True(bookshelf.Tick(Crafter, 1, done: false));
        Assert.False(bookshelf.TodosOf(Crafter)[0].IsDone);

        Assert.True(bookshelf.Scrap(Crafter, 1));
        Assert.False(bookshelf.Scrap(Crafter, 1));
        Assert.Equal([new Todo(2, "Cite a todo")], bookshelf.TodosOf(Crafter));
        Assert.Equal(new Todo(3, "Ship it"), bookshelf.Jot(Crafter, "Ship it"));
    }

    [Fact]
    public void JotsNoTodoForAProjectOffTheShelf()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        Assert.Null(bookshelf.Jot(Crafter, "Draw the panel"));
        Assert.Empty(bookshelf.TodosOf(Crafter));
    }

    [Fact]
    public void GivesAndTakesASkillAndKeepsItThroughReopening()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning));
        var bookshelf = new Bookshelf(store, new SettingsInMemory(), () => Morning.AddHours(1));

        bookshelf.Give(Crafter, "commit", given: true);
        bookshelf.Open(Crafter);

        Assert.True(bookshelf.Has(Crafter, "commit"));
        Assert.False(bookshelf.Has(Beacon, "commit"));
        Assert.Equal(["commit"], store.Saved[0].Skills);

        bookshelf.Give(Crafter, "commit", given: false);

        Assert.False(bookshelf.Has(Crafter, "commit"));
        Assert.Empty(store.Saved[0].Skills);
    }
}
