namespace Crafter.Core.Conversations.Decisions;

public sealed record Deny(string? Reason = null) : Decision;
