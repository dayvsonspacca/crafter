using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Tests;

internal sealed class ScriptedSession(IReadOnlyList<Reply> replies, bool hangs = false) : IAgentSession
{
    public string? SessionId => null;

    public Configuration Adjustments { get; private set; } = Configuration.Empty;

    public void Adjust(Configuration adjustments) => Adjustments = adjustments;

    public void Decide(string request, Decision decision)
    {
    }

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        foreach (var reply in replies)
        {
            yield return reply;
        }

        if (!hangs)
        {
            yield break;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stop);
        }
        catch (OperationCanceledException)
        {
        }

        yield return new Stopped();
    }
}
