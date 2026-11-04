using System.Runtime.CompilerServices;
using System.Text;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Conversations;

public sealed class Conversation
{
    private readonly IAgentSession agent;

    private readonly List<Turn> turns;

    public Conversation(IAgentSession agent, IEnumerable<Turn> history)
    {
        this.agent = agent;
        turns = [.. history];

        if (agent is IWorksInBackground background)
        {
            background.JobsChanged += (_, jobs) => JobsChanged?.Invoke(this, jobs);
            background.Woke += (_, replies) => Woke?.Invoke(this, Recorded(replies));
        }
    }

    public Conversation(IAgentSession agent)
        : this(agent, [])
    {
    }

    public event EventHandler<IReadOnlyList<Job>>? JobsChanged;

    public event EventHandler<IAsyncEnumerable<Reply>>? Woke;

    public IReadOnlyList<Turn> Turns => turns;

    public string? SessionId => agent.SessionId;

    public IReadOnlyList<Job> Jobs => agent is IWorksInBackground background ? background.Jobs : [];

    public void Decide(string request, Decision decision) => agent.Decide(request, decision);

    public void Stop(string job)
    {
        if (agent is IWorksInBackground background)
        {
            background.Stop(job);
        }
    }

    public void Dismiss()
    {
        if (agent is IWorksInBackground background)
        {
            background.Dismiss();
        }
    }

    public IAsyncEnumerable<Reply> Ask(Prompt prompt, Configuration preferred, CancellationToken stop = default)
    {
        turns.Add(new Asked(prompt));

        return Recorded(agent.Answer(prompt, preferred, stop));
    }

    private async IAsyncEnumerable<Reply> Recorded(IAsyncEnumerable<Reply> replies, [EnumeratorCancellation] CancellationToken stop = default)
    {
        var answer = new StringBuilder();
        var writing = new StringBuilder();
        List<Reply> steps = [];
        Ending? ending = null;

        void Wrap()
        {
            if (writing.Length > 0)
            {
                steps.Add(new Wrote(writing.ToString()));
                writing.Clear();
            }
        }

        await foreach (var reply in replies.WithCancellation(stop))
        {
            switch (reply)
            {
                case Wrote wrote:
                    answer.Append(wrote.Fragment);
                    writing.Append(wrote.Fragment);
                    break;
                case Acting acting:
                    Wrap();
                    steps.Add(acting);
                    break;
                case Acted acted:
                    Wrap();
                    steps.Add(acted.Output.Length > Answered.LongestOutput ? acted with { Output = acted.Output[..Answered.LongestOutput] + "\n…" } : acted);
                    break;
                case Ending ended:
                    ending = ended;
                    break;
            }

            yield return reply;
        }

        Wrap();

        if (ending is null)
        {
            ending = new Failed("The agent went silent before finishing its answer.");

            yield return ending;
        }

        // An answer of words alone is its text; steps are kept only when tools came between them.
        turns.Add(new Answered(answer.ToString(), ending) { Steps = steps.Any(step => step is not Wrote) ? steps : [] });
    }
}
