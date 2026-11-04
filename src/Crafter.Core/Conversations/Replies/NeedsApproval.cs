namespace Crafter.Core.Conversations.Replies;

public sealed record NeedsApproval(string Id, Tool Tool, string Summary, string? Reason = null, string? Remembers = null) : Reply;
