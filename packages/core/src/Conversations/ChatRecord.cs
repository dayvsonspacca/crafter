using Crafter.Core.Agents;

namespace Crafter.Core.Conversations;

public sealed record ChatRecord(ChatId Id, AgentId Agent, ChatTitle Title, string? SessionId, IReadOnlyList<Turn> Turns);
