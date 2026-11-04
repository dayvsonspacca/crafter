using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Testing;

public sealed class ScriptedSession(params Reply[] replies) : IAgentSession
{
    public List<Prompt> Heard { get; } = [];

    public string? SessionId { get; set; }

    public List<Configuration> Configurations { get; } = [];

    public List<(string Request, Decision Decision)> Decisions { get; } = [];

    public bool Hangs { get; init; }

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

        if (!Hangs)
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
