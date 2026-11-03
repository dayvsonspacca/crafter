namespace Crafter.Core.Agents;

public sealed record Ready(string Version, string? Account) : Readiness;
