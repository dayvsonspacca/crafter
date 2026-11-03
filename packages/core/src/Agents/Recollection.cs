namespace Crafter.Core.Agents;

public sealed record Recollection(IReadOnlyList<PastChat> Latest, int Total);
