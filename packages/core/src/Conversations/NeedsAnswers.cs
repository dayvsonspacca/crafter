namespace Crafter.Core.Conversations;

public sealed record NeedsAnswers(string Id, IReadOnlyList<Question> Questions) : Reply;
