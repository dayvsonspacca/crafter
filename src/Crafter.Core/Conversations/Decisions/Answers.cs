namespace Crafter.Core.Conversations.Decisions;

public sealed record Answers(IReadOnlyList<Answer> Given) : Decision;
