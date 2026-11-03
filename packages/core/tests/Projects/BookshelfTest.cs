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
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning)), () => Morning);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
    }

    [Fact]
    public void ShelvesANewProjectAtTheEnd()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning));
        var bookshelf = new Bookshelf(store, () => Morning.AddHours(1));

        bookshelf.Open(Beacon);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
        Assert.Equal([new Shelved(Crafter, Morning), new Shelved(Beacon, Morning.AddHours(1))], store.Saved);
    }

    [Fact]
    public void KeepsTheProjectInPlaceWhenReopened()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning));
        var bookshelf = new Bookshelf(store, () => Morning.AddHours(2));

        bookshelf.Open(Crafter);

        Assert.Equal([Crafter, Beacon], bookshelf.Projects);
        Assert.Equal(new Shelved(Crafter, Morning.AddHours(2)), store.Saved[0]);
    }

    [Fact]
    public void KnowsWhichProjectWasOpenedLast()
    {
        var bookshelf = new Bookshelf(
            new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning.AddHours(3)), new Shelved(Reborn, Morning.AddHours(1))),
            () => Morning);

        Assert.Equal(Beacon, bookshelf.LastOpened);
    }

    [Fact]
    public void KnowsNoLastProjectWhenEmpty()
    {
        Assert.Null(new Bookshelf(new ShelfInMemory(), () => Morning).LastOpened);
    }

    [Fact]
    public void RemovesAProject()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning));
        var bookshelf = new Bookshelf(store, () => Morning);

        bookshelf.Remove(Crafter);

        Assert.Equal([Beacon], bookshelf.Projects);
        Assert.Equal([new Shelved(Beacon, Morning)], store.Saved);
    }

    [Fact]
    public void LeavesTheStoreAloneWhenRemovingAnUnknownProject()
    {
        var store = new ShelfInMemory(new Shelved(Crafter, Morning));

        new Bookshelf(store, () => Morning).Remove(Beacon);

        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void FindsEverythingByRecencyWithoutAQuery()
    {
        var bookshelf = new Bookshelf(
            new ShelfInMemory(new Shelved(Crafter, Morning), new Shelved(Beacon, Morning.AddHours(1))),
            () => Morning);

        var found = bookshelf.Search("  ");

        Assert.Equal([Beacon, Crafter], found.Select(each => each.Project));
        Assert.All(found, each => Assert.Empty(each.NameHits));
    }

    [Fact]
    public void FindsTheLettersOfTheQueryInTheName()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), () => Morning);

        var found = Assert.Single(bookshelf.Search("CRE"));

        Assert.Equal([0, 1, 5], found.NameHits);
        Assert.Empty(found.FolderHits);
    }

    [Fact]
    public void FindsTheLettersInTheFolderWhenTheNameMisses()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Beacon, Morning)), () => Morning);

        var found = Assert.Single(bookshelf.Search("me/b"));

        Assert.Empty(found.NameHits);
        Assert.Equal("me/b", string.Concat(found.FolderHits.Select(at => Beacon.Folder[at])));
    }

    [Fact]
    public void BreaksTiesByRecency()
    {
        var bookshelf = new Bookshelf(
            new ShelfInMemory(new Shelved(Reborn, Morning), new Shelved(Crafter, Morning.AddHours(1))),
            () => Morning);

        Assert.Equal([Crafter, Reborn], bookshelf.Search("re").Select(each => each.Project));
    }

    [Fact]
    public void PrefersAHitInTheNameOverAFresherHitInTheFolder()
    {
        var notes = new Project("/home/hero/books/notes");
        var bookshelf = new Bookshelf(
            new ShelfInMemory(new Shelved(Beacon, Morning), new Shelved(notes, Morning.AddHours(1))),
            () => Morning);

        Assert.Equal([Beacon, notes], bookshelf.Search("bo").Select(each => each.Project));
    }

    [Fact]
    public void FindsNothingWhenNoLetterMatches()
    {
        var bookshelf = new Bookshelf(new ShelfInMemory(new Shelved(Crafter, Morning)), () => Morning);

        Assert.Empty(bookshelf.Search("zz"));
    }
}
