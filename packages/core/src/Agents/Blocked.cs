namespace Crafter.Core.Agents;

public sealed record Blocked(string State, string Detail, string? Remedy) : Readiness(State, Detail);
