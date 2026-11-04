namespace Crafter.Core.Conversations.Replies;

// Something an approval keeps allowed once it is always allowed, in the agent's words, and how far that reaches.
public sealed record Rule(string What, Reach Reach);
