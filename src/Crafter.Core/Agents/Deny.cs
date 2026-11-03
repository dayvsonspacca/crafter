namespace Crafter.Core.Agents;

public sealed record Deny(string? Reason = null) : Decision;
