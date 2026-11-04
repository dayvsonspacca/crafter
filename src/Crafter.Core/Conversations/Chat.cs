using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
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

    public event EventHandler<IReadOnlyList<Job>>? JobsChanged
    {
        add => conversation.JobsChanged += value;
        remove => conversation.JobsChanged -= value;
    }

    public event EventHandler<IAsyncEnumerable<Reply>>? Woke
    {
        add => conversation.Woke += value;
        remove => conversation.Woke -= value;
    }

    public IReadOnlyList<Job> Jobs => conversation.Jobs;

    public void Decide(string request, Decision decision) => conversation.Decide(request, decision);

    public void Stop(string job) => conversation.Stop(job);

    public void Dismiss() => conversation.Dismiss();

    public bool Runs(IAgentSession session) => conversation.Runs(session);

    public IAsyncEnumerable<Reply> Ask(Prompt prompt, Configuration preferred, CancellationToken stop = default)
    {
        if (conversation.Turns.Count == 0)
        {
            Title = ChatTitle.From(prompt);
        }

        return conversation.Ask(prompt, preferred, stop);
    }

    public ChatRecord Record() => new(Id, Agent, Title, conversation.SessionId, [.. conversation.Turns]);
}
