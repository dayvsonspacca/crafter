namespace Crafter.Core.Agents;

public sealed class Crew
{
    private readonly List<IAgent> roster;

    private List<IAgent> hired = [];

    public Crew(IEnumerable<IAgent> roster)
    {
        this.roster = [.. roster];
        if (this.roster.Count == 0)
        {
            throw new ArgumentException("A crew needs at least one agent to hire.", nameof(roster));
        }
    }

    public IReadOnlyList<IAgent> Roster => roster;

    public IReadOnlyList<IAgent> Hired => hired;

    public bool IsHiring => hired.Count == 0;

    public IAgent Default => hired.Count > 0 ? hired[0] : roster[0];

    public void Hire(IEnumerable<AgentId> agents)
    {
        HashSet<AgentId> wanted = [.. agents];

        hired = roster.FindAll(agent => wanted.Contains(agent.Id));
    }

    public bool IsHired(AgentId id) => hired.Exists(agent => agent.Id == id);

    public IAgent Find(AgentId id) => roster.Find(agent => agent.Id == id) ?? Default;
}
