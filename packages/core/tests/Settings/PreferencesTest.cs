using Crafter.Core.Agents;
using Crafter.Core.Settings;

namespace Crafter.Core.Tests.Settings;

public sealed class PreferencesTest
{
    [Fact]
    public void StartsAtHome()
    {
        Assert.Equal(new Preferences("/home/hero"), Preferences.Default("/home/hero"));
    }

    [Fact]
    public void ForgetsTheTrailingSeparatorOfTheRoot()
    {
        Assert.Equal("/home/hero/Projetos", new Preferences("/home/hero/Projetos/").ProjectsRoot);
    }

    [Fact]
    public void RefusesABlankRoot()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Preferences(" "));
    }

    [Fact]
    public void StartsWithoutAgents()
    {
        Assert.Empty(Preferences.Default("/home/hero").Agents);
    }

    [Fact]
    public void KeepsEachAgentOnce()
    {
        var chosen = Preferences.Default("/home/hero") with { Agents = [new AgentId("claude-code"), new AgentId("claude-code")] };

        Assert.Equal([new AgentId("claude-code")], chosen.Agents);
    }

    [Fact]
    public void ComparesTheAgentsByWhatTheyHold()
    {
        var one = Preferences.Default("/home/hero") with { Agents = [new AgentId("claude-code")] };
        var other = Preferences.Default("/home/hero") with { Agents = [new AgentId("claude-code")] };

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.NotEqual(one, Preferences.Default("/home/hero"));
        Assert.False(one.Equals(null));
    }

    [Fact]
    public void StartsWithoutAChosenTheme()
    {
        Assert.Null(Preferences.Default("/home/hero").Theme);
    }

    [Theory]
    [InlineData(" deepslate ", "deepslate")]
    [InlineData("  ", null)]
    public void KeepsTheChosenTheme(string chosen, string? kept)
    {
        Assert.Equal(kept, (Preferences.Default("/home/hero") with { Theme = chosen }).Theme);
    }

    [Fact]
    public void ComparesTheTheme()
    {
        Assert.NotEqual(Preferences.Default("/home/hero") with { Theme = "nether" }, Preferences.Default("/home/hero"));
    }

    [Fact]
    public void StartsWithAnEmptyConfigurationForEachAgent()
    {
        Assert.Same(Configuration.Empty, Preferences.Default("/home/hero").ConfigurationOf(new AgentId("claude-code")));
    }

    [Fact]
    public void KeepsTheConfigurationOfEachAgent()
    {
        var claude = new AgentId("claude-code");
        var configured = Preferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "opus"));

        Assert.Equal("opus", configured.ConfigurationOf(claude)["model"]);
        Assert.Equal(configured, Preferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "opus")));
        Assert.NotEqual(configured, Preferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "sonnet")));
        Assert.NotEqual(configured, Preferences.Default("/home/hero"));
    }

    [Fact]
    public void ForgetsAnEmptyConfiguration()
    {
        var claude = new AgentId("claude-code");

        Assert.Empty(Preferences.Default("/home/hero").Configure(claude, Configuration.Empty).Configurations);
    }
}
