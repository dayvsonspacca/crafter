using Crafter.Core.Agents;

namespace Crafter.Core.Tests.Agents;

public sealed class CrewTest
{
    private static readonly StandInAgent Claude = new("claude-code");

    private static readonly StandInAgent Codex = new("codex");

    [Fact]
    public void StartsHiringWithNobodyHired()
    {
        var crew = new Crew([Claude, Codex]);

        Assert.True(crew.IsHiring);
        Assert.Empty(crew.Hired);
    }

    [Fact]
    public void LeadsWithTheFirstOnTheRosterWhileHiring()
    {
        Assert.Same(Claude, new Crew([Claude, Codex]).Default);
    }

    [Fact]
    public void HiresTheChosenAgentsInRosterOrder()
    {
        var crew = new Crew([Claude, Codex]);

        crew.Hire([Codex.Id, Claude.Id]);

        Assert.False(crew.IsHiring);
        Assert.Equal<IAgent>([Claude, Codex], crew.Hired);
    }

    [Fact]
    public void LeadsWithTheFirstHiredAgent()
    {
        var crew = new Crew([Claude, Codex]);

        crew.Hire([Codex.Id]);

        Assert.Same(Codex, crew.Default);
        Assert.True(crew.IsHired(Codex.Id));
        Assert.False(crew.IsHired(Claude.Id));
    }

    [Fact]
    public void IgnoresAgentsOffTheRoster()
    {
        var crew = new Crew([Claude]);

        crew.Hire([new AgentId("gone")]);

        Assert.True(crew.IsHiring);
    }

    [Fact]
    public void HiringAgainReplacesTheCrew()
    {
        var crew = new Crew([Claude, Codex]);

        crew.Hire([Claude.Id, Codex.Id]);
        crew.Hire([Codex.Id]);

        Assert.Equal<IAgent>([Codex], crew.Hired);
    }

    [Fact]
    public void FindsAnyAgentOnTheRosterByItsId()
    {
        var crew = new Crew([Claude, Codex]);

        crew.Hire([Claude.Id]);

        Assert.Same(Codex, crew.Find(Codex.Id));
    }

    [Fact]
    public void FallsBackToTheDefaultForAnUnknownAgent()
    {
        Assert.Same(Claude, new Crew([Claude, Codex]).Find(new AgentId("gone")));
    }

    [Fact]
    public void ListsItsRoster()
    {
        Assert.Equal<IAgent>([Claude, Codex], new Crew([Claude, Codex]).Roster);
    }

    [Fact]
    public void RefusesAnEmptyRoster()
    {
        Assert.Throws<ArgumentException>(() => new Crew([]));
    }
}
