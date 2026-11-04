using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Preferences;

namespace Crafter.Core.Tests.Preferences;

public sealed class UserPreferencesTest
{
    [Fact]
    public void StartsAtHome()
    {
        Assert.Equal(new UserPreferences("/home/hero"), UserPreferences.Default("/home/hero"));
    }

    [Fact]
    public void ForgetsTheTrailingSeparatorOfTheRoot()
    {
        Assert.Equal("/home/hero/Projetos", new UserPreferences("/home/hero/Projetos/").ProjectsRoot);
    }

    [Fact]
    public void RefusesABlankRoot()
    {
        Assert.ThrowsAny<ArgumentException>(() => new UserPreferences(" "));
    }

    [Fact]
    public void StartsWithoutAgents()
    {
        Assert.Empty(UserPreferences.Default("/home/hero").Agents);
    }

    [Fact]
    public void KeepsEachAgentOnce()
    {
        var chosen = UserPreferences.Default("/home/hero") with { Agents = [new AgentId("claude-code"), new AgentId("claude-code")] };

        Assert.Equal([new AgentId("claude-code")], chosen.Agents);
    }

    [Fact]
    public void ComparesTheAgentsByWhatTheyHold()
    {
        var one = UserPreferences.Default("/home/hero") with { Agents = [new AgentId("claude-code")] };
        var other = UserPreferences.Default("/home/hero") with { Agents = [new AgentId("claude-code")] };

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.NotEqual(one, UserPreferences.Default("/home/hero"));
        Assert.False(one.Equals(null));
    }

    [Fact]
    public void StartsWithoutAChosenTheme()
    {
        Assert.Null(UserPreferences.Default("/home/hero").Theme);
    }

    [Theory]
    [InlineData(" deepslate ", "deepslate")]
    [InlineData("  ", null)]
    public void KeepsTheChosenTheme(string chosen, string? kept)
    {
        Assert.Equal(kept, (UserPreferences.Default("/home/hero") with { Theme = chosen }).Theme);
    }

    [Fact]
    public void ComparesTheTheme()
    {
        Assert.NotEqual(UserPreferences.Default("/home/hero") with { Theme = "nether" }, UserPreferences.Default("/home/hero"));
    }

    [Fact]
    public void StartsWithAnEmptyConfigurationForEachAgent()
    {
        Assert.Same(Configuration.Empty, UserPreferences.Default("/home/hero").ConfigurationOf(new AgentId("claude-code")));
    }

    [Fact]
    public void KeepsTheConfigurationOfEachAgent()
    {
        var claude = new AgentId("claude-code");
        var configured = UserPreferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "opus"));

        Assert.Equal("opus", configured.ConfigurationOf(claude)["model"]);
        Assert.Equal(configured, UserPreferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "opus")));
        Assert.NotEqual(configured, UserPreferences.Default("/home/hero").Configure(claude, Configuration.Empty.With("model", "sonnet")));
        Assert.NotEqual(configured, UserPreferences.Default("/home/hero"));
    }

    [Fact]
    public void ForgetsAnEmptyConfiguration()
    {
        var claude = new AgentId("claude-code");

        Assert.Empty(UserPreferences.Default("/home/hero").Configure(claude, Configuration.Empty).Configurations);
    }
}
