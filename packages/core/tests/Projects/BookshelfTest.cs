using Crafter.Core.Projects;

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
    public void KnowsWhichProjectWasOpenedLast()
    {
        var bookshelf = new Bookshelf(
            new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning.AddHours(3)), new Shelved(Reborn, Morning.AddHours(1))),
            new SettingsInMemory(),
            () => Morning);

        Assert.Equal(Beacon, bookshelf.LastOpened);
    }

    [Fact]
    public void KnowsNoLastProjectWhenEmpty()
    {
        Assert.Null(new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning).LastOpened);
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
}
