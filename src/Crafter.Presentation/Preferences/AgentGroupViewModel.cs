using Crafter.Presentation.Agents;

namespace Crafter.Presentation.Preferences;

public sealed class AgentGroupViewModel(AgentPanelViewModel agent) : PreferenceGroupViewModel(agent.AgentName)
{
    public AgentPanelViewModel Agent { get; } = agent;
}
