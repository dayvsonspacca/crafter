using System.Diagnostics;
using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCodeSession(string workingDirectory, Func<Configuration, ClaudeCodeOptions> options, string? resumeFrom = null) : IAgentSession
{
    private readonly List<string> allowed = [];

    private readonly List<string> directories = [];

    private readonly Dictionary<string, TaskCompletionSource<Decision>> waiting = [];

    private string? sessionId = resumeFrom;

    public ClaudeCodeSession(string workingDirectory, string executable = "claude", string? resumeFrom = null)
        : this(workingDirectory, _ => new ClaudeCodeOptions(executable), resumeFrom)
    {
    }

    public string? SessionId => sessionId;

    public void Decide(string request, Decision decision)
    {
        if (waiting.Remove(request, out var decided))
        {
            decided.TrySetResult(decision);
        }
    }

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, [EnumeratorCancellation] CancellationToken stop = default)
    {
        var now = options(configuration);
        var process = ClaudeCodeProcess.Start(now.Executable, workingDirectory, [.. now.Arguments(sessionId), .. Granted()]);
        if (process is null)
        {
            yield return new Failed($"Could not start '{now.Executable}'. Is Claude Code installed and on the PATH?");

            yield break;
        }

        using (process)
        {
            var complaints = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await Send(process, ClaudeCodeProtocol.Asking(prompt));

            var transcript = new Transcript(workingDirectory);
            var output = new Output(process, stop);
            var ended = false;
            var heardEverything = false;

            try
            {
                while (!stop.IsCancellationRequested && await output.Next() is { } line)
                {
                    var replies = transcript.Hear(line);
                    sessionId = transcript.SessionId ?? sessionId;

                    foreach (var reply in replies)
                    {
                        if (RequestOf(reply) is { } request)
                        {
                            var decided = Waiting(request);

                            yield return reply;

                            var (decision, withdrawn) = await output.Await(decided, request);
                            waiting.Remove(request);
                            if (withdrawn)
                            {
                                yield return new Withdrawn(request);

                                continue;
                            }

                            if (decision is null)
                            {
                                break;
                            }

                            await Respond(process, transcript, request, decision, asked: reply is NeedsAnswers);

                            continue;
                        }

                        if (reply is Ending)
                        {
                            ended = true;
                            Close(process);
                        }

                        yield return reply;
                    }
                }

                heardEverything = !stop.IsCancellationRequested;
            }
            finally
            {
                waiting.Clear();
                if (!heardEverything)
                {
                    ClaudeCodeProcess.Kill(process);
                }
            }

            if (ended)
            {
                yield break;
            }

            if (stop.IsCancellationRequested)
            {
                yield return new Stopped();

                yield break;
            }

            Close(process);
            await process.WaitForExitAsync(CancellationToken.None);
            var complaint = (await complaints).Trim();

            yield return new Failed(
                complaint.Length > 0 ? complaint : $"Claude Code exited with code {process.ExitCode} without an answer.");
        }
    }

    private static string? RequestOf(Reply reply) => reply switch
    {
        NeedsApproval approval => approval.Id,
        NeedsAnswers asked => asked.Id,
        _ => null,
    };

    private Task<Decision> Waiting(string request)
    {
        var decided = new TaskCompletionSource<Decision>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[request] = decided;

        return decided.Task;
    }

    private Task Respond(Process process, Transcript transcript, string request, Decision decision, bool asked)
    {
        var suggestions = transcript.SuggestionsOf(request);
        if (decision is AlwaysAllow)
        {
            Grant(suggestions);
        }

        return Send(process, ClaudeCodeProtocol.Answering(request, decision, transcript.InputOf(request), suggestions, asked));
    }

    private IEnumerable<string> Granted()
    {
        if (allowed.Count > 0)
        {
            yield return "--allowedTools";
            foreach (var rule in allowed)
            {
                yield return rule;
            }
        }

        if (directories.Count > 0)
        {
            yield return "--add-dir";
            foreach (var directory in directories)
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

    private static async Task Send(Process process, string line)
    {
        try
        {
            await process.StandardInput.WriteLineAsync(line);
            await process.StandardInput.FlushAsync();
        }
        catch (Exception closed) when (closed is IOException or ObjectDisposedException)
        {
        }
    }

    private static void Close(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
        }
    }

    private sealed class Output(Process process, CancellationToken stop)
    {
        private readonly Queue<string> early = [];

        private Task<string?>? reading;

        public async Task<string?> Next() => early.TryDequeue(out var line) ? line : await Take();

        public async Task<(Decision? Decision, bool Withdrawn)> Await(Task<Decision> decided, string request)
        {
            if (early.FirstOrDefault(line => Transcript.Withdraws(line) == request) is { } withdrawal)
            {
                Forget(withdrawal);

                return (null, true);
            }

            while (true)
            {
                reading ??= Read();
                if (await Task.WhenAny(decided, reading) == decided)
                {
                    return (await decided, false);
                }

                if (await Take() is not { } line)
                {
                    return (null, false);
                }

                if (Transcript.Withdraws(line) == request)
                {
                    return (null, true);
                }

                early.Enqueue(line);
            }
        }

        private void Forget(string withdrawal)
        {
            var kept = early.Where(line => !ReferenceEquals(line, withdrawal)).ToList();
            early.Clear();
            foreach (var line in kept)
            {
                early.Enqueue(line);
            }
        }

        private async Task<string?> Take()
        {
            var line = await (reading ?? Read());
            reading = null;

            return line;
        }

        private async Task<string?> Read()
        {
            try
            {
                return await process.StandardOutput.ReadLineAsync(stop);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}
