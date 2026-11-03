using Crafter.Core.Conversations;

namespace Crafter.Core.Agents;

public sealed record PastChat(string SessionId, ChatTitle Title, DateTimeOffset LastActive);
