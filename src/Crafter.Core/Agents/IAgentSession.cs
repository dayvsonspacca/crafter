using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Agents;

public interface IAgentSession
{
    string? SessionId { get; }

    void Decide(string request, Decision decision);

    IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, CancellationToken stop = default);
}
