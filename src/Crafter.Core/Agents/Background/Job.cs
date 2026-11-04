namespace Crafter.Core.Agents.Background;

public sealed record Job(string Id, JobKind Kind, string Description, string? Output = null);
