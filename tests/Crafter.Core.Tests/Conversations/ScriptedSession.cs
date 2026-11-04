using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Tests.Conversations;

internal sealed class ScriptedSession(params Reply[] replies) : IAgentSession
{
    public List<Prompt> Heard { get; } = [];

    public string? SessionId { get; set; }

    public List<Configuration> Configurations { get; } = [];

    public List<(string Request, Decision Decision)> Decisions { get; } = [];

    public void Decide(string request, Decision decision) => Decisions.Add((request, decision));

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, [EnumeratorCancellation] CancellationToken stop = default)
    {
        Heard.Add(prompt);
        Configurations.Add(configuration);

        foreach (var reply in replies)
        {
            await Task.Yield();

            yield return reply;
        }
    }
}
