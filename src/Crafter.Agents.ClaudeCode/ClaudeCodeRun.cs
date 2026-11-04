using System.Diagnostics;
using System.Threading.Channels;
using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Agents.Background;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode;

internal sealed class ClaudeCodeRun
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    private readonly Process process;

    private readonly ClaudeCodeTranscript transcript;

    private readonly Task<string> complaints;

    private readonly SemaphoreSlim writing = new(1, 1);

    private readonly Lock gate = new();

    private readonly List<Turn> pending = [];

    private Turn? current;

    private bool tracksLifecycle;

    private bool closed;

    private int requests;

    private ClaudeCodeRun(Process process, ClaudeCodeOptions options, string folder)
    {
        this.process = process;
        Options = options;
        transcript = new ClaudeCodeTranscript(folder);
        complaints = process.StandardError.ReadToEndAsync(CancellationToken.None);
    }

    public event Action<string>? Known;

    public event Action<IReadOnlyList<Job>>? JobsChanged;

    public event Action<Turn>? Woke;

    public ClaudeCodeOptions Options { get; }

    public Task Reading { get; private set; } = Task.CompletedTask;

    public IReadOnlyList<Job> Jobs
    {
        get
        {
            lock (gate)
            {
                return [.. transcript.Jobs];
            }
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (gate)
            {
                return !closed;
            }
        }
    }

    public static ClaudeCodeRun? Start(ClaudeCodeOptions options, string folder, IEnumerable<string> arguments) =>
        ClaudeCodeProcess.Start(options.Executable, folder, arguments) is { } process ? new ClaudeCodeRun(process, options, folder) : null;

    public void Begin() => Reading = Task.Run(Read);

    public async Task Finish()
    {
        if (await Task.WhenAny(Reading, Task.Delay(Patience)) != Reading)
        {
            Kill();
            await Reading;
        }
    }

    public Turn Ask(Prompt prompt)
    {
        var turn = new Turn(Guid.NewGuid().ToString());
        lock (gate)
        {
            pending.Add(turn);
        }

        _ = Send(ClaudeCodeProtocol.Asking(prompt, turn.Id));

        return turn;
    }

    public string? Respond(string request, Decision decision)
    {
        string input;
        string? suggestions;
        lock (gate)
        {
            input = transcript.InputOf(request);
            suggestions = transcript.SuggestionsOf(request);
        }

        _ = Send(ClaudeCodeProtocol.Answering(request, decision, input, suggestions));

        return suggestions;
    }

    public void Stop(string job) => _ = Send(ClaudeCodeProtocol.Stopping(Request(), job));

    public async Task Halt(Turn turn)
    {
        lock (gate)
        {
            pending.Remove(turn);
        }

        if (Jobs.Count > 0)
        {
            await Send(ClaudeCodeProtocol.Interrupting(Request()));
            if (await Task.WhenAny(turn.Done, Task.Delay(Patience)) == turn.Done)
            {
                return;
            }
        }

        Kill();
    }

    public void Kill() => ClaudeCodeProcess.Kill(process);

    private string Request() => $"crafter-{Interlocked.Increment(ref requests)}";

    private async Task Read()
    {
        while (await Line() is { } line)
        {
            var (moved, jobs, woken) = Hear(line);
            if (woken is not null)
            {
                Woke?.Invoke(woken);
            }

            if (moved)
            {
                JobsChanged?.Invoke(jobs);
            }
        }

        await Ended();
    }

    private (bool Moved, IReadOnlyList<Job> Jobs, Turn? Woken) Hear(string line)
    {
        Turn? woken = null;
        lock (gate)
        {
            var replies = transcript.Hear(line);
            if (transcript.SessionId is { } session)
            {
                Known?.Invoke(session);
            }

            if (transcript.Started is { } started)
            {
                tracksLifecycle = true;
                current = pending.Find(turn => turn.Id == started) ?? new Turn(started);
                pending.Remove(current);
            }

            if (current is null && (transcript.Opened || replies.Count > 0))
            {
                if (!tracksLifecycle && pending.Count > 0)
                {
                    current = pending[0];
                    pending.RemoveAt(0);
                }
                else if (transcript.Opened || replies.Any(reply => reply is not Ending))
                {
                    current = woken = new Turn(Guid.NewGuid().ToString());
                }
            }

            foreach (var reply in replies)
            {
                if (current is not { } turn)
                {
                    continue;
                }

                turn.Hear(reply);
                if (reply is Ending)
                {
                    current = null;
                    Settle();
                }
            }

            return (transcript.JobsMoved, [.. transcript.Jobs], woken);
        }
    }

    private void Settle()
    {
        if (transcript.Jobs.Count == 0 && pending.Count == 0 && !closed)
        {
            closed = true;
            _ = Close();
        }
    }

    private async Task Ended()
    {
        await process.WaitForExitAsync(CancellationToken.None);
        var complaint = (await complaints).Trim();
        var reason = complaint.Length > 0 ? complaint : $"Claude Code exited with code {process.ExitCode} without an answer.";

        bool hadJobs;
        List<Turn> unfinished;
        lock (gate)
        {
            closed = true;
            hadJobs = transcript.Jobs.Count > 0;
            unfinished = [.. pending];
            if (current is { } turn)
            {
                unfinished.Insert(0, turn);
            }

            pending.Clear();
            current = null;
        }

        foreach (var turn in unfinished)
        {
            turn.Hear(new Failed(reason));
        }

        if (hadJobs)
        {
            JobsChanged?.Invoke([]);
        }

        process.Dispose();
    }

    private async Task<string?> Line()
    {
        try
        {
            return await process.StandardOutput.ReadLineAsync();
        }
        catch (Exception closed) when (closed is IOException or ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task Send(string line)
    {
        await writing.WaitAsync();
        try
        {
            await process.StandardInput.WriteLineAsync(line);
            await process.StandardInput.FlushAsync();
        }
        catch (Exception closed) when (closed is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        finally
        {
            writing.Release();
        }
    }

    private async Task Close()
    {
        await writing.WaitAsync();
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception closed) when (closed is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        finally
        {
            writing.Release();
        }
    }

    internal sealed class Turn(string id)
    {
        private readonly Channel<Reply> replies = Channel.CreateUnbounded<Reply>(new UnboundedChannelOptions { SingleReader = true });

        private readonly TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Id { get; } = id;

        public Task Done => done.Task;

        public ChannelReader<Reply> Replies => replies.Reader;

        public void Hear(Reply reply)
        {
            if (!replies.Writer.TryWrite(reply) || reply is not Ending)
            {
                return;
            }

            replies.Writer.TryComplete();
            done.TrySetResult();
        }
    }
}
