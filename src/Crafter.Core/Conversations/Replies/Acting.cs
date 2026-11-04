namespace Crafter.Core.Conversations.Replies;

public sealed record Acting(string Id, Tool Tool, string Summary, Changes? Changes = null) : Reply;
