namespace Crafter.Core.Conversations.Decisions;

public sealed record Answers(IReadOnlyDictionary<string, string> Chosen) : Decision;
