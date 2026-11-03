using System.Runtime.CompilerServices;
using Crafter.Core.Conversations;

namespace Crafter.Core.Tests.Conversations;

internal sealed class ScriptedAgent(params Reply[] replies) : IAgent
{
    public List<Prompt> Heard { get; } = [];

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        Heard.Add(prompt);

        foreach (var reply in replies)
        {
            await Task.Yield();

            yield return reply;
        }
    }
}
