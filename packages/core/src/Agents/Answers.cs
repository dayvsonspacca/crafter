namespace Crafter.Core.Agents;

public sealed record Answers(IReadOnlyDictionary<string, string> Chosen) : Decision;
