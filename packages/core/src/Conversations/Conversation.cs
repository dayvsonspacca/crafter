using System.Runtime.CompilerServices;
using System.Text;

namespace Crafter.Core.Conversations;

public sealed class Conversation(IAgent agent)
{
    private readonly List<Turn> turns = [];

    public IReadOnlyList<Turn> Turns => turns;

    public async IAsyncEnumerable<Reply> Ask(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        turns.Add(new Asked(prompt));
        var answer = new StringBuilder();
        Ending? ending = null;

        await foreach (var reply in agent.Answer(prompt, stop))
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
