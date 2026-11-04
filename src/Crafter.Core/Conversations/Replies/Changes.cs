namespace Crafter.Core.Conversations.Replies;

// The lines a change to a file adds and removes; a file written whole has no lines removed to count.
public sealed record Changes(int Added, int? Removed = null);
