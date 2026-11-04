namespace Crafter.Core.Conversations.Replies;

public sealed record Refused(string? Reason = null) : Ending;
