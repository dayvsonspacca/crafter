using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;

namespace Crafter.Core.Conversations;

public sealed record ChatRecord(ChatId Id, AgentId Agent, ChatTitle Title, string? SessionId, IReadOnlyList<Turn> Turns)
{
    public Configuration Adjustments { get; init; } = Configuration.Empty;
}
