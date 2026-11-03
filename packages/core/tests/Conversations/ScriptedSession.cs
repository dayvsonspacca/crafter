using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Core.Tests.Conversations;

internal sealed class ScriptedSession(params Reply[] replies) : IAgentSession
{
    public List<Prompt> Heard { get; } = [];

    public string? SessionId { get; set; }

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
