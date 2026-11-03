using Crafter.Core.Agents;
using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Agents;

internal sealed class StandInAgent(string id) : IAgent
{
    public AgentId Id { get; } = new(id);

    public string Name => id;

    public string Summary => $"Stands in for {id}.";

    public IReadOnlyList<Setting> Settings(Configuration configuration) => [];

    public Task<Readiness> Check(Configuration configuration, CancellationToken stop = default) => Task.FromResult<Readiness>(new Ready("Ready", "1.0"));

    public IAgentSession Begin(Project project, string? resumeFrom = null) => throw new NotSupportedException();
}
