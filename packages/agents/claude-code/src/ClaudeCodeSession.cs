using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCodeSession(string workingDirectory, Func<Configuration, ClaudeCodeOptions> options, string? resumeFrom = null) : IAgentSession
{
    private const string Refusal = "The user denied this in Crafter. Don't try it again; take another way or ask them how to proceed.";

    private const string Silence = "The user chose not to answer. Carry on with your best judgment or ask in plain text.";

    private readonly List<string> allowed = [];

    private readonly List<string> directories = [];

    private readonly Dictionary<string, TaskCompletionSource<Decision>> waiting = [];

    private string? sessionId = resumeFrom;

    public ClaudeCodeSession(string workingDirectory, string executable = "claude", string? resumeFrom = null)
        : this(workingDirectory, _ => new ClaudeCodeOptions(executable), resumeFrom)
    {
    }

    public Configuration Adjustments { get; private set; } = Configuration.Empty;

    public string? SessionId => sessionId;

    public void Adjust(Configuration adjustments) => Adjustments = adjustments;

    public void Decide(string request, Decision decision)
    {
        if (waiting.Remove(request, out var decided))
        {
            decided.TrySetResult(decision);
        }
    }

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        var now = options(Adjustments);
        var process = ClaudeCodeProcess.Start(now.Executable, workingDirectory, [.. now.Arguments(sessionId), .. Granted()]);
        if (process is null)
        {
            yield return new Failed($"Could not start '{now.Executable}'. Is Claude Code installed and on the PATH?");

            yield break;
        }

        using (process)
        {
            var complaints = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await Send(process, Asking(prompt));

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
                        if (reply is NeedsApproval or NeedsAnswers)
                        {
                            var request = reply is NeedsApproval approval ? approval.Id : ((NeedsAnswers)reply).Id;
                            var decided = new TaskCompletionSource<Decision>(TaskCreationOptions.RunContinuationsAsynchronously);
                            waiting[request] = decided;

                            yield return reply;

                            var (decision, withdrawn) = await output.Await(decided.Task, request);
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

                            if (decision is AlwaysAllow)
                            {
                                Grant(transcript.SuggestionsOf(request));
                            }

                            await Send(process, Answering(request, decision, transcript.InputOf(request), transcript.SuggestionsOf(request), reply is NeedsAnswers));

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

    private static string Asking(Prompt prompt) =>
        Json(writer =>
        {
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", prompt.Text);
            writer.WriteEndObject();
        });

    private static string Answering(string request, Decision decision, string input, string? suggestions, bool asked) =>
        Json(writer =>
        {
            writer.WriteString("type", "control_response");
            writer.WriteStartObject("response");
            writer.WriteString("subtype", "success");
            writer.WriteString("request_id", request);
            writer.WriteStartObject("response");
            switch (decision)
            {
                case Deny deny:
                    writer.WriteString("behavior", "deny");
                    writer.WriteString("message", Refused(deny.Reason, asked));
                    break;
                case Answers answers:
                    writer.WriteString("behavior", "allow");
                    writer.WritePropertyName("updatedInput");
                    Answered(writer, input, answers);
                    break;
                default:
                    writer.WriteString("behavior", "allow");
                    writer.WritePropertyName("updatedInput");
                    writer.WriteRawValue(input);
                    if (decision is AlwaysAllow && suggestions is not null)
                    {
                        writer.WritePropertyName("updatedPermissions");
                        writer.WriteRawValue(suggestions);
                    }

                    break;
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    private static string Refused(string? reason, bool asked) =>
        reason?.Trim() is { Length: > 0 } said
            ? $"The user {(asked ? "chose not to answer" : "denied this in Crafter")} and said: {said}"
            : asked ? Silence : Refusal;

    private static void Answered(Utf8JsonWriter writer, string input, Answers answers)
    {
        using var document = JsonDocument.Parse(input);
        writer.WriteStartObject();
        foreach (var property in document.RootElement.EnumerateObject().Where(property => property.Name != "answers"))
        {
            property.WriteTo(writer);
        }

        writer.WriteStartObject("answers");
        foreach (var (question, answer) in answers.Chosen)
        {
            writer.WriteString(question, answer);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
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
