using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

public sealed class JsonProjectSettingsStoreTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-project-").FullName;

    private string File => Path.Combine(folder, ".crafter", "settings.json");

    [Fact]
    public void HasNoSettingsWithoutAFile()
    {
        Assert.Null(new JsonProjectSettingsStore().Load(folder));
    }

    [Fact]
    public void LoadsWhatItSavedInTheProject()
    {
        var store = new JsonProjectSettingsStore();

        store.Save(folder, new ProjectSettings(new ProjectName("Crafter · desktop")));

        Assert.Equal(new ProjectSettings(new ProjectName("Crafter · desktop")), store.Load(folder));
        Assert.Contains("\"name\": \"Crafter · desktop\"", System.IO.File.ReadAllText(File));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"name":" "}""")]
    public void HasNoSettingsWhenTheFileIsUnreadable(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, content);

        Assert.Null(new JsonProjectSettingsStore().Load(folder));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
