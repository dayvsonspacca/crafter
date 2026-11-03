using Crafter.Desktop.Agents;

namespace Crafter.Desktop.Settings;

public sealed class AgentGroup(AgentPanel agent) : PreferenceGroup(agent.AgentName)
{
    public AgentPanel Agent { get; } = agent;
}
