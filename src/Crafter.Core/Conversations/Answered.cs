using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Conversations;

public sealed record Answered(string Text, Ending Ending) : Turn;
