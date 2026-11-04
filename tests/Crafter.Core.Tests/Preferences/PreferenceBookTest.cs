using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Preferences;
using Crafter.Testing;

namespace Crafter.Core.Tests.Preferences;

public sealed class PreferenceBookTest
{
    private static readonly UserPreferences Stored = new("/home/hero/Projetos") { Theme = "warped" };

    private static readonly AgentId Claude = new("claude-code");

    [Fact]
    public void StartsWithWhatWasStored()
    {
        Assert.Equal(Stored, new PreferenceBook(new PreferencesInMemory(Stored)).Current);
    }

    [Fact]
    public void ReadsTheStoreOnlyOnce()
    {
        var store = new PreferencesInMemory(Stored);
        var book = new PreferenceBook(store);

        _ = book.Current;
        book.ChooseTheme("copper");
        _ = book.Current;

        Assert.Equal(1, store.Loads);
    }

    [Fact]
    public void KeepsTheHiredAgents()
    {
        var store = new PreferencesInMemory(Stored);

        new PreferenceBook(store).Hire([Claude]);

        Assert.Equal([Claude], store.Saved.Agents);
    }

    [Fact]
    public void KeepsAnAgentsConfiguration()
    {
        var store = new PreferencesInMemory(Stored);
        var configuration = Configuration.Empty.With("model", "opus");

        new PreferenceBook(store).Configure(Claude, configuration);

        Assert.Equal(configuration, store.Saved.ConfigurationOf(Claude));
    }

    [Fact]
    public void KeepsTheProjectsRoot()
    {
        var store = new PreferencesInMemory(Stored);

        new PreferenceBook(store).MoveProjectsRoot("/home/hero/code/");

        Assert.Equal("/home/hero/code", store.Saved.ProjectsRoot);
    }

    [Fact]
    public void KeepsTheTheme()
    {
        var store = new PreferencesInMemory(Stored);

        new PreferenceBook(store).ChooseTheme("copper");

        Assert.Equal("copper", store.Saved.Theme);
    }

    [Fact]
    public void ChangesBuildOnEachOther()
    {
        var store = new PreferencesInMemory(Stored);
        var book = new PreferenceBook(store);

        book.ChooseTheme("copper");
        book.Hire([Claude]);

        Assert.Equal(Stored with { Theme = "copper", Agents = [Claude] }, store.Saved);
        Assert.Equal(store.Saved, book.Current);
    }

    [Fact]
    public void SkipsSavingWhatDidNotChange()
    {
        var store = new PreferencesInMemory(Stored);

        new PreferenceBook(store).ChooseTheme("warped");

        Assert.Equal(0, store.Saves);
    }
}
