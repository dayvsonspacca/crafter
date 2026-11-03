using Crafter.Core.Agents;

namespace Crafter.Core.Conversations;

public sealed class Chat(ChatId id, AgentId agent, ChatTitle title, Conversation conversation)
{
    public ChatId Id { get; } = id;

    public AgentId Agent { get; } = agent;

    public ChatTitle Title { get; private set; } = title;

    public IReadOnlyList<Turn> Turns => conversation.Turns;

    public IAsyncEnumerable<Reply> Ask(Prompt prompt, CancellationToken stop = default)
    {
        if (conversation.Turns.Count == 0)
        {
            Title = ChatTitle.From(prompt);
        }

        return conversation.Ask(prompt, stop);
    }

    public ChatRecord Record() => new(Id, Agent, Title, conversation.SessionId, [.. conversation.Turns]);
}
