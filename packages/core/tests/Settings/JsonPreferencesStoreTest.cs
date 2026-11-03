using Crafter.Core.Agents;
using Crafter.Core.Settings;

namespace Crafter.Core.Tests.Settings;

public sealed class JsonPreferencesStoreTest : IDisposable
{
    private static readonly Preferences Defaults = Preferences.Default("/home/hero");

    private readonly string folder = Directory.CreateTempSubdirectory("crafter-preferences-").FullName;

    private string File => Path.Combine(folder, "crafter", "preferences.json");

    [Fact]
    public void StartsWithTheDefaultsWithoutAFile()
    {
        Assert.Equal(Defaults, new JsonPreferencesStore(File, Defaults).Load());
    }

    [Fact]
    public void LoadsWhatItSaved()
    {
        var chosen = new Preferences("/home/hero/Projetos") { Theme = "warped", Agents = [new AgentId("claude-code")] }
            .Configure(new AgentId("claude-code"), Configuration.Empty.With("model", "opus").With("tools", "edit"));

        new JsonPreferencesStore(File, Defaults).Save(chosen);

        Assert.Equal(chosen, new JsonPreferencesStore(File, Defaults).Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"projectsRoot":""}""")]
    [InlineData("""{"projectsRoot":"/home/hero","agents":[" "]}""")]
    [InlineData("""{"projectsRoot":"/home/hero","configurations":{"claude-code":[]}}""")]
    [InlineData("""{"projectsRoot":"/home/hero","configurations":{"claude-code":{"model":4}}}""")]
    public void StartsWithTheDefaultsWhenTheFileIsUnreadable(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, content);

        Assert.Equal(Defaults, new JsonPreferencesStore(File, Defaults).Load());
    }

    [Fact]
    public void WritesTheAgentsByTheirIds()
    {
        new JsonPreferencesStore(File, Defaults).Save(Defaults with { Agents = [new AgentId("claude-code")] });

        Assert.Contains("\"claude-code\"", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void ReadsPreferencesWrittenBeforeRepositorySearchWasFixed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, """{"projectsRoot":"/home/hero/Projetos","findsRepositories":false,"searchDepth":6}""");

        Assert.Equal(new Preferences("/home/hero/Projetos"), new JsonPreferencesStore(File, Defaults).Load());
    }

    [Fact]
    public void LivesInTheUserConfig()
    {
        Assert.NotNull(JsonPreferencesStore.InUserConfig());
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
