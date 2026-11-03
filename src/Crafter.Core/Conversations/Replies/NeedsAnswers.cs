namespace Crafter.Core.Conversations.Replies;

public sealed record NeedsAnswers(string Id, IReadOnlyList<Question> Questions) : Reply;
