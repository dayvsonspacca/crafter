using Crafter.Presentation.Agents;

namespace Crafter.Presentation.Settings;

public sealed class AgentGroup(AgentPanel agent) : PreferenceGroup(agent.AgentName)
{
    public AgentPanel Agent { get; } = agent;
}
