namespace Crafter.Core.Conversations.Decisions;

// Questions left unanswered: not a denial, the agent carries on without them.
public sealed record Skip(string? Reason = null) : Decision;
