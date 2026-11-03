using Crafter.Core.Agents;
using Crafter.Core.Settings;

namespace Crafter.Core.Tests.Settings;

public sealed class PreferencesTest
{
    [Fact]
    public void StartsAtHomeFindingRepositoriesFourLevelsDeep()
    {
        Assert.Equal(new Preferences("/home/hero", true, 4), Preferences.Default("/home/hero"));
    }

    [Fact]
    public void ForgetsTheTrailingSeparatorOfTheRoot()
    {
        Assert.Equal("/home/hero/Projetos", new Preferences("/home/hero/Projetos/", true, 4).ProjectsRoot);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(20, 8)]
    public void KeepsTheDepthWithinBounds(int asked, int kept)
    {
        Assert.Equal(kept, new Preferences("/home/hero", true, asked).SearchDepth);
        Assert.Equal(kept, (Preferences.Default("/home/hero") with { SearchDepth = asked }).SearchDepth);
    }

    [Fact]
    public void RefusesABlankRoot()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Preferences(" ", true, 4));
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
}
