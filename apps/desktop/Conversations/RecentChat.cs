using Crafter.Core.Agents;

namespace Crafter.Desktop.Conversations;

public sealed class RecentChat(IAgent agent, PastChat past, ConversationScreen? open, DateTimeOffset now)
{
    public IAgent Agent { get; } = agent;

    public PastChat Past { get; } = past;

    public string Title => Past.Title.Text;

    public string When { get; } = Elapsed.Since(past.LastActive, now);

    public bool IsOpen => open is not null;

    public string Tag => IsOpen ? "open" : Agent.Name;

    public bool IsCrafting => open?.State == ChatState.Crafting;

    public bool IsUnread => open?.State == ChatState.Unread;

    public bool IsIdle => !IsCrafting && !IsUnread;
}
