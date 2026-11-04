using Crafter.Core.Agents;
using Crafter.Presentation.Conversations;

namespace Crafter.Presentation.Desks;

public sealed class RecentChat(IAgent agent, PastChat past, ConversationScreen? open, DateTimeOffset now)
{
    public IAgent Agent { get; } = agent;

    public PastChat Past { get; } = past;

    public string Title => Past.Title.Text;

    public string When { get; } = Elapsed.Since(past.LastActive, now);

    public string Last => Past.LastPrompt is { } prompt ? $"You: {prompt.Text.Split('\n')[0].Trim()}" : "Single prompt";

    public bool IsOpen => open is not null;

    public bool IsLast { get; init; }

    public string Action => IsOpen ? "open" : "Resume →";

    public bool IsCrafting => open?.State == ChatState.Crafting;

    public bool IsUnread => open?.State == ChatState.Unread;

    public bool IsIdle => !IsCrafting && !IsUnread;
}
