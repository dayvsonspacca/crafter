using Crafter.Core.Projects;

namespace Crafter.Core.Agents;

public interface IAgent
{
    AgentId Id { get; }

    string Name { get; }

    string Summary { get; }

    IReadOnlyList<Setting> Settings { get; }

    Configuration Configuration { get; }

    void Configure(Configuration configuration);

    Task<Readiness> Check(CancellationToken stop = default);

    IAgentSession Begin(Project project, string? resumeFrom = null);
}
