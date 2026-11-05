using Crafter.Core.Projects;
using Crafter.Storage;

namespace Crafter.Storage.Tests;

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
    public void KeepsTheAxiomsInOrder()
    {
        var store = new JsonProjectSettingsStore();
        var settings = new ProjectSettings(new ProjectName("Crafter")) { Axioms = ["Never add comments", "Always write tests"] };

        store.Save(folder, settings);

        Assert.Equal(settings, store.Load(folder));
        Assert.Contains("\"Never add comments\"", System.IO.File.ReadAllText(File));
        store.Save(folder, new ProjectSettings(new ProjectName("Crafter")));
        Assert.DoesNotContain("axioms", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void KeepsTheTodosInAFileOfTheirOwn()
    {
        var store = new JsonProjectSettingsStore();
        var todos = Path.Combine(folder, ".crafter", "todos.json");
        Assert.Empty(store.LoadTodos(folder));

        store.SaveTodos(folder, [new Todo(1, "Draw the panel", IsDone: true), new Todo(3, "Cite a todo")]);

        Assert.Equal([new Todo(1, "Draw the panel", IsDone: true), new Todo(3, "Cite a todo")], store.LoadTodos(folder));
        Assert.Contains("\"text\": \"Draw the panel\"", System.IO.File.ReadAllText(todos));

        store.SaveTodos(folder, []);

        Assert.Empty(store.LoadTodos(folder));
        Assert.False(System.IO.File.Exists(todos));
    }

    [Fact]
    public void SetsUnreadableTodosAside()
    {
        var todos = Path.Combine(folder, ".crafter", "todos.json");
        Directory.CreateDirectory(Path.GetDirectoryName(todos)!);
        System.IO.File.WriteAllText(todos, "{ not json");

        Assert.Empty(new JsonProjectSettingsStore().LoadTodos(folder));
        Assert.False(System.IO.File.Exists(todos));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(todos)!, "todos.json.*.unreadable"));
    }

    [Fact]
    public void KeepsTheSummaryInAMarkdownFileOfItsOwn()
    {
        var store = new JsonProjectSettingsStore();
        Assert.Null(store.LoadSummary(folder));

        store.SaveSummary(folder, "  Crafter is my IDE.\n\nIt is written in C#.  ");

        Assert.Equal("Crafter is my IDE.\n\nIt is written in C#.", store.LoadSummary(folder));
        Assert.Equal("Crafter is my IDE.\n\nIt is written in C#.\n", System.IO.File.ReadAllText(Path.Combine(folder, ".crafter", "summary.md")));

        store.SaveSummary(folder, " ");

        Assert.Null(store.LoadSummary(folder));
        Assert.False(System.IO.File.Exists(Path.Combine(folder, ".crafter", "summary.md")));
    }

    [Fact]
    public void FindsTheAgentsInstructionsTheProjectBrings()
    {
        var store = new JsonProjectSettingsStore(["AGENTS.md", "CLAUDE.md"]);
        Assert.Null(store.FindInstructions(folder));

        System.IO.File.WriteAllText(Path.Combine(folder, "CLAUDE.md"), "Crafter is my IDE.\n");
        Assert.Equal(new AgentInstructions("CLAUDE.md", "Crafter is my IDE."), store.FindInstructions(folder));

        System.IO.File.WriteAllText(Path.Combine(folder, "AGENTS.md"), "\n");
        Assert.Equal("CLAUDE.md", store.FindInstructions(folder)?.File);

        System.IO.File.WriteAllText(Path.Combine(folder, "AGENTS.md"), "Crafter is an IDE.");
        Assert.Equal("AGENTS.md", store.FindInstructions(folder)?.File);
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

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"name":" "}""")]
    public void KeepsAnUnreadableFileAside(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, content);
        var store = new JsonProjectSettingsStore();

        store.Load(folder);
        store.Save(folder, new ProjectSettings(new ProjectName("crafter")));

        Assert.Equal(content, System.IO.File.ReadAllText(Aside.Of(File)));
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
