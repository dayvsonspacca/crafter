using System.Runtime.CompilerServices;
using System.Text;
using Crafter.Core.Agents;

namespace Crafter.Core.Conversations;

public sealed class Conversation(IAgentSession agent, IEnumerable<Turn> history, Configuration? adjustments = null)
{
    private readonly List<Turn> turns = [.. history];

    public Conversation(IAgentSession agent)
        : this(agent, [])
    {
    }

    public IReadOnlyList<Turn> Turns => turns;

    public string? SessionId => agent.SessionId;

    public Configuration Adjustments { get; private set; } = adjustments ?? Configuration.Empty;

    public void Adjust(Configuration adjustments) => Adjustments = adjustments;

    public void Decide(string request, Decision decision) => agent.Decide(request, decision);

    public async IAsyncEnumerable<Reply> Ask(Prompt prompt, Configuration preferred, [EnumeratorCancellation] CancellationToken stop = default)
    {
        turns.Add(new Asked(prompt));
        var answer = new StringBuilder();
        Ending? ending = null;

        await foreach (var reply in agent.Answer(prompt, Adjustments.Over(preferred), stop))
        {
            switch (reply)
            {
                case Wrote wrote:
                    answer.Append(wrote.Fragment);
                    break;
                case Ending ended:
                    ending = ended;
                    break;
            }

            yield return reply;
        }

        if (ending is null)
        {
            ending = new Failed("The agent went silent before finishing its answer.");

            yield return ending;
        }

        turns.Add(new Answered(answer.ToString(), ending));
    }
}
