using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;

namespace Crafter.Testing;

public sealed class StandInAgent(string id = "stand-in") : IAgent, IKeepsHistory
{
    public AgentId Id { get; } = new(id);

    public string Name => id;

    public string Summary => $"Stands in for {id}.";

    public Func<IAgentSession> Sessions { get; init; } = () => new ScriptedSession(new Finished());

    public Readiness Readiness { get; init; } = new Ready("Ready", "1.0");

    public IReadOnlyList<Turn> Replays { get; init; } = [];

    public IReadOnlyList<Setting> Settings(Configuration configuration) => [];

    public Task<Readiness> Check(Configuration configuration, CancellationToken stop = default) => Task.FromResult(Readiness);

    public IAgentSession Begin(Project project, string? resumeFrom = null) => Sessions();

    Task<Recollection> IKeepsHistory.History(Project project, int latest, CancellationToken stop) => Task.FromResult(new Recollection([], 0));

    public Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default) => Task.FromResult(Replays);
}
