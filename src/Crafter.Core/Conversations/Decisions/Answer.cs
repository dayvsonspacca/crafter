using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Conversations.Decisions;

// What the user made of one question: the picks they chose and, if they wrote their own, its words.
public sealed record Answer(Question Question, IReadOnlyList<Pick> Picked, string? Written = null);
