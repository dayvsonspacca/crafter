namespace Crafter.Core.Conversations;

public sealed record Acting(string Id, string Tool, string Summary, string? Stat = null) : Reply;
