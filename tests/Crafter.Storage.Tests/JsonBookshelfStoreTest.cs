using Crafter.Core.Projects;
using Crafter.Storage;

namespace Crafter.Storage.Tests;

public sealed class JsonBookshelfStoreTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-bookshelf-").FullName;

    private string File => Path.Combine(folder, "crafter", "projects.json");

    [Fact]
    public void StartsEmptyWithoutAFile()
    {
        Assert.Empty(new JsonBookshelfStore(File).Load());
    }

    [Fact]
    public void LoadsWhatItSaved()
    {
        Shelved[] shelf =
        [
            new(new Project("/home/hero/crafter"), new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero)),
            new(new Project("/home/hero/beacon"), new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero)),
        ];

        new JsonBookshelfStore(File).Save(shelf);

        Assert.Equal(shelf, new JsonBookshelfStore(File).Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{"folder":"","lastOpened":"2026-11-03T09:00:00+00:00"}]""")]
    public void StartsEmptyWhenTheFileIsUnreadable(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, content);

        Assert.Empty(new JsonBookshelfStore(File).Load());
    }

    [Fact]
    public void StartsEmptyWhenTheFileHoldsNothing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, "null");

        Assert.Empty(new JsonBookshelfStore(File).Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{"folder":"","lastOpened":"2026-11-03T09:00:00+00:00"}]""")]
    public void KeepsAnUnreadableFileAside(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, content);
        var store = new JsonBookshelfStore(File);

        store.Load();
        store.Save([]);

        Assert.Equal(content, System.IO.File.ReadAllText(Aside.Of(File)));
    }

    [Fact]
    public void LeavesOnlyTheFileBehindWhenSaving()
    {
        var store = new JsonBookshelfStore(File);

        store.Save([]);
        store.Save([]);

        Assert.Equal([File], Directory.GetFiles(Path.GetDirectoryName(File)!));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
