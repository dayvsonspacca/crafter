using System.Runtime.CompilerServices;
using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCodeSession(
    string workingDirectory,
    Func<Configuration, ClaudeCodeOptions> options,
    string? resumeFrom = null,
    Func<IReadOnlyList<ToolServer>>? servers = null,
    Func<ClaudeCodeSkillShelf?>? skills = null) : IAgentSession, IWorksInBackground
{
    private readonly List<string> allowed = [];

    private readonly List<string> directories = [];

    private readonly HashSet<string> questions = [];

    private readonly Lock gate = new();

    private ClaudeCodeRun? run;

    // The skills the running session was started with.
    private ClaudeCodeSkillShelf? shelf;

    private string? sessionId = resumeFrom;

    public ClaudeCodeSession(string workingDirectory, string executable = "claude", string? resumeFrom = null)
        : this(workingDirectory, _ => new ClaudeCodeOptions(executable), resumeFrom)
    {
    }

    public event EventHandler<IReadOnlyList<Job>>? JobsChanged;

    public event EventHandler<IAsyncEnumerable<Reply>>? Woke;

    public string? SessionId => sessionId;

    public IReadOnlyList<Job> Jobs => run?.Jobs ?? [];

    public void Decide(string request, Decision decision)
    {
        if (run is not { } live)
        {
            return;
        }

        bool asked;
        lock (gate)
        {
            asked = questions.Remove(request);
        }

        var suggestions = live.Respond(request, decision, asked);
        if (decision is AlwaysAllow)
        {
            Grant(suggestions);
        }
    }

    public void Stop(string job) => run?.Stop(job);

    public void Dismiss() => run?.Kill();

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, [EnumeratorCancellation] CancellationToken stop = default)
    {
        if (await Running(options(configuration)) is not { } live)
        {
            yield return new Failed($"Could not start '{options(configuration).Executable}'. Is Claude Code installed and on the PATH?");

            yield break;
        }

        var turn = live.Ask(ClaudeCodeSkills.Calling(prompt, shelf));
        await foreach (var reply in Heard(turn, stop))
        {
            yield return reply;
        }

        if (stop.IsCancellationRequested && !turn.Done.IsCompleted)
        {
            await live.Halt(turn);

            yield return new Stopped();
        }
    }

    private async IAsyncEnumerable<Reply> Heard(ClaudeCodeRun.Turn turn, [EnumeratorCancellation] CancellationToken stop = default)
    {
        while (true)
        {
            try
            {
                if (!await turn.Replies.WaitToReadAsync(stop))
                {
                    yield break;
                }
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            while (turn.Replies.TryRead(out var reply))
            {
                if (reply is NeedsAnswers asked)
                {
                    lock (gate)
                    {
                        questions.Add(asked.Id);
                    }
                }

                yield return reply;
            }
        }
    }

    private async Task<ClaudeCodeRun?> Running(ClaudeCodeOptions wanted)
    {
        if (run is { IsOpen: true } live && (live.Options == wanted || live.Jobs.Count > 0))
        {
            return live;
        }

        if (run is { } previous)
        {
            if (previous.IsOpen)
            {
                previous.Kill();
            }

            await previous.Finish();
        }

        shelf = skills?.Invoke();
        var started = ClaudeCodeRun.Start(wanted, workingDirectory, [.. wanted.Arguments(sessionId), .. Granted()]);
        if (started is null)
        {
            return null;
        }

        started.Known += session => sessionId = session;
        started.JobsChanged += jobs => JobsChanged?.Invoke(this, jobs);
        started.Woke += turn => Woke?.Invoke(this, Heard(turn));
        started.Begin();

        return run = started;
    }

    // Asked for again on every start, so a server or a skill granted or revoked since takes effect then.
    // Claude Code's own skills and slash commands stay off: Crafter's skills are the only ones.
    private IEnumerable<string> Granted()
    {
        yield return "--disable-slash-commands";
        if (shelf is { } given)
        {
            yield return "--append-system-prompt";
            yield return ClaudeCodeSkills.Catalog(given);
        }

        var connected = servers?.Invoke() ?? [];
        if (connected.Count > 0)
        {
            yield return "--mcp-config";
            yield return ClaudeCodeMcp.Config(connected);
        }

        string[] rules = [.. allowed, .. connected.SelectMany(server => server.FreeTools.Select(tool => $"mcp__{server.Name}__{tool}"))];
        if (rules.Length > 0)
        {
            yield return "--allowedTools";
            foreach (var rule in rules)
            {
                yield return rule;
            }
        }

        string[] reachable = [.. directories, .. shelf is { } copied ? [copied.Folder] : Array.Empty<string>()];
        if (reachable.Length > 0)
        {
            yield return "--add-dir";
            foreach (var directory in reachable)
            {
                yield return directory;
            }
        }
    }

    private void Grant(string? suggestions)
    {
        var (rules, folders) = ClaudeCodePermissions.Granted(suggestions);
        allowed.AddRange(rules.Except(allowed));
        directories.AddRange(folders.Except(directories));
    }
}
