using Crafter.Core.Agents;
using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Agents;

internal sealed class StandInAgent(string id) : IAgent
{
    public AgentId Id { get; } = new(id);

    public string Name => id;

    public string Summary => $"Stands in for {id}.";

    public Task<Readiness> Check(CancellationToken stop = default) => Task.FromResult<Readiness>(new Ready("1.0", null));

    public IAgentSession Begin(Project project, string? resumeFrom = null) => throw new NotSupportedException();
}
