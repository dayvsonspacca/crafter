using Crafter.Core.Conversations;

namespace Crafter.Core.Agents;

public interface IAgentSession
{
    string? SessionId { get; }

    IAsyncEnumerable<Reply> Answer(Prompt prompt, CancellationToken stop = default);
}
