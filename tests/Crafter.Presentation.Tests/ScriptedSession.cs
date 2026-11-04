using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Tests;

internal sealed class ScriptedSession(IReadOnlyList<Reply> replies, bool hangs = false) : IAgentSession
{
    public string? SessionId => null;

    public List<Configuration> Configurations { get; } = [];

    public void Decide(string request, Decision decision)
    {
    }

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, [EnumeratorCancellation] CancellationToken stop = default)
    {
        Configurations.Add(configuration);

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
