using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Tests;

internal sealed class StandInAgent(Func<IAgentSession> sessions, Readiness? readiness = null) : IAgent, IKeepsHistory
{
    public StandInAgent()
        : this(() => new ScriptedSession([new Finished()]))
    {
    }

    public AgentId Id { get; } = new("stand-in");

    public string Name => "Stand-in";

    public string Summary => "Stands in for a real agent.";

    public IReadOnlyList<Setting> Settings(Configuration configuration) => [];

    public IReadOnlyList<Turn> Replays { get; init; } = [];

    public Task<Readiness> Check(Configuration configuration, CancellationToken stop = default) => Task.FromResult(readiness ?? new Ready("Ready", "1.0"));

    public IAgentSession Begin(Project project, string? resumeFrom = null) => sessions();

    Task<Recollection> IKeepsHistory.History(Project project, int latest, CancellationToken stop) => Task.FromResult(new Recollection([], 0));

    public Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default) => Task.FromResult(Replays);
}
