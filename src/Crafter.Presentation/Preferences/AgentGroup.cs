using Crafter.Presentation.Agents;

namespace Crafter.Presentation.Preferences;

public sealed class AgentGroup(AgentPanel agent) : PreferenceGroup(agent.AgentName)
{
    public AgentPanel Agent { get; } = agent;
}
