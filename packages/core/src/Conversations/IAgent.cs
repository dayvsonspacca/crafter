namespace Crafter.Core.Conversations;

public interface IAgent
{
    IAsyncEnumerable<Reply> Answer(Prompt prompt, CancellationToken stop = default);
}
