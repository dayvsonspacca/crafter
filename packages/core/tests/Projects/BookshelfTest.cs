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
    public void RenamesAProjectInPlace()
    {
        var settings = new SettingsInMemory();
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), settings, () => Morning);

        var renamed = bookshelf.Rename(Crafter, new ProjectName("Crafter"));

        Assert.Equal("Crafter", renamed.Name.Text);
        Assert.Equal(["Crafter", "beacon"], bookshelf.Projects.Select(project => project.Name.Text));
        Assert.Equal(new ProjectName("Crafter"), bookshelf.NameOf(Crafter.Folder));
    }

    [Fact]
    public void RenamesAProjectOffTheShelfWithoutShelvingIt()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => Morning);

        bookshelf.Rename(Reborn, new ProjectName("Reborn"));

        Assert.Empty(bookshelf.Projects);
        Assert.Equal(new ProjectName("Reborn"), bookshelf.NameOf(Reborn.Folder));
    }
}
