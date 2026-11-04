namespace Crafter.Core.Agents.Background;

// A job prints when its agent can show what it printed.
public sealed record Job(string Id, JobKind Kind, string Description, bool Prints = false);
