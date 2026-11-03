namespace Crafter.Core.Agents;

public sealed record Ready(string State, string Detail) : Readiness(State, Detail);
