namespace Crafter.Core.Conversations;

public sealed record NeedsApproval(string Id, string Tool, string Summary, string? Reason = null, string? Remembers = null) : Reply;
