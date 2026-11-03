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

    [Fact]
    public void KeepsTheIcon()
    {
        var store = new JsonProjectSettingsStore();

        store.Save(folder, new ProjectSettings(new ProjectName("Crafter"), "assets/logo.png"));

        Assert.Equal("assets/logo.png", store.Load(folder)?.Icon);
    }

    [Fact]
    public void KeepsACopyOfTheIconInTheProject()
    {
        var image = Image("Logo.PNG");

        var icon = new JsonProjectSettingsStore().AdoptIcon(folder, image);

        Assert.Equal(".crafter/icon.png", icon);
        Assert.Equal("png bytes", System.IO.File.ReadAllText(Path.Combine(folder, ".crafter", "icon.png")));
        Assert.True(System.IO.File.Exists(image));
    }

    [Fact]
    public void ReplacesTheIconItKeptBefore()
    {
        var store = new JsonProjectSettingsStore();

        store.AdoptIcon(folder, Image("logo.png"));
        store.AdoptIcon(folder, Image("photo.jpg"));

        Assert.Equal(["icon.jpg"], Directory.GetFiles(Path.Combine(folder, ".crafter"), "icon.*").Select(Path.GetFileName));
    }

    [Fact]
    public void AdoptsTheIconItAlreadyKeeps()
    {
        var store = new JsonProjectSettingsStore();
        store.AdoptIcon(folder, Image("logo.png"));

        Assert.Equal(".crafter/icon.png", store.AdoptIcon(folder, Path.Combine(folder, ".crafter", "icon.png")));
        Assert.True(System.IO.File.Exists(Path.Combine(folder, ".crafter", "icon.png")));
    }

    [Fact]
    public void DropsTheIconItKept()
    {
        var store = new JsonProjectSettingsStore();
        store.Save(folder, new ProjectSettings(new ProjectName("Crafter")));
        store.AdoptIcon(folder, Image("logo.png"));

        store.DropIcon(folder);

        Assert.Empty(Directory.GetFiles(Path.Combine(folder, ".crafter"), "icon.*"));
        Assert.True(System.IO.File.Exists(File));
    }

    [Fact]
    public void DropsNothingWhenItKeepsNothing()
    {
        new JsonProjectSettingsStore().DropIcon(folder);

        Assert.False(Directory.Exists(Path.Combine(folder, ".crafter")));
    }

    [Fact]
    public void LeavesTheIconOutWhenThereIsNone()
    {
        new JsonProjectSettingsStore().Save(folder, new ProjectSettings(new ProjectName("Crafter")));

        Assert.DoesNotContain("icon", System.IO.File.ReadAllText(File));
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

    private string Image(string name)
    {
        var elsewhere = Path.Combine(folder, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var image = Path.Combine(elsewhere, name);
        System.IO.File.WriteAllText(image, Path.GetExtension(name).TrimStart('.').ToLowerInvariant() + " bytes");

        return image;
    }
}
