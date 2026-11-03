using Crafter.Core.Conversations;

namespace Crafter.Core.Agents;

public interface IAgentSession
{
    string? SessionId { get; }

    Configuration Adjustments { get; }

    void Adjust(Configuration adjustments);

    IAsyncEnumerable<Reply> Answer(Prompt prompt, CancellationToken stop = default);
}
