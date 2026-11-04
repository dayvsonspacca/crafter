using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Conversations;

public sealed class Chat(ChatId id, AgentId agent, ChatTitle title, Conversation conversation)
{
    public ChatId Id { get; } = id;

    public AgentId Agent { get; } = agent;

    public ChatTitle Title { get; private set; } = title;

    public IReadOnlyList<Turn> Turns => conversation.Turns;

    public string? SessionId => conversation.SessionId;

    public Configuration Adjustments => conversation.Adjustments;

    public void Adjust(Configuration adjustments) => conversation.Adjust(adjustments);

    public void Decide(string request, Decision decision) => conversation.Decide(request, decision);

    public IAsyncEnumerable<Reply> Ask(Prompt prompt, Configuration preferred, CancellationToken stop = default)
    {
        if (conversation.Turns.Count == 0)
        {
            Title = ChatTitle.From(prompt);
        }

        return conversation.Ask(prompt, preferred, stop);
    }

    public ChatRecord Record() => new(Id, Agent, Title, conversation.SessionId, [.. conversation.Turns]) { Adjustments = conversation.Adjustments };
}
