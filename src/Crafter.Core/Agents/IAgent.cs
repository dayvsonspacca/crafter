using Crafter.Core.Agents.Options;
using Crafter.Core.Projects;

namespace Crafter.Core.Agents;

public interface IAgent
{
    AgentId Id { get; }

    string Name { get; }

    string Summary { get; }

    // The instructions files this agent reads on its own in a project, which Crafter turns off.
    IReadOnlyList<string> InstructionFiles { get; }

    IReadOnlyList<Setting> Settings(Configuration configuration);

    Task<Readiness> Check(Configuration configuration, CancellationToken stop = default);

    IAgentSession Begin(Project project, string? resumeFrom = null);
}
