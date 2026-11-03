using Crafter.Core.Agents;
using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Agents;

internal sealed class StandInAgent(string id) : IAgent
{
    public AgentId Id { get; } = new(id);

    public string Name => id;

    public string Summary => $"Stands in for {id}.";

    public IReadOnlyList<Setting> Settings => [];

    public Configuration Configuration { get; private set; } = Configuration.Empty;

    public void Configure(Configuration configuration) => Configuration = configuration;

    public Task<Readiness> Check(CancellationToken stop = default) => Task.FromResult<Readiness>(new Ready("Ready", "1.0"));

    public IAgentSession Begin(Project project, string? resumeFrom = null) => throw new NotSupportedException();
}
