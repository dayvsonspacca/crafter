using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCodeSession(string workingDirectory, Func<Configuration, ClaudeCodeOptions> options, string? resumeFrom = null) : IAgentSession
{
    private readonly HashSet<string> trusted = [];

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
        var process = ClaudeCodeProcess.Start(now.Executable, workingDirectory, now.Arguments(sessionId));
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
            var ended = false;
            var heardEverything = false;

            try
            {
                while (!stop.IsCancellationRequested && await Listen(process, stop) is { } line)
                {
                    var replies = transcript.Hear(line);
                    sessionId = transcript.SessionId ?? sessionId;

                    foreach (var reply in replies)
                    {
                        if (reply is NeedsApproval approval)
                        {
                            if (!trusted.Contains(approval.Tool))
                            {
                                var decided = new TaskCompletionSource<Decision>(TaskCreationOptions.RunContinuationsAsynchronously);
                                waiting[approval.Id] = decided;

                                yield return approval;

                                var decision = await Await(decided.Task, stop);
                                if (decision is null)
                                {
                                    break;
                                }

                                if (decision is AllowTool)
                                {
                                    trusted.Add(approval.Tool);
                                }

                                await Send(process, Answering(approval.Id, decision, transcript.InputOf(approval.Id)));
                            }
                            else
                            {
                                await Send(process, Answering(approval.Id, new Allow(), transcript.InputOf(approval.Id)));
                            }

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

    private static string Asking(Prompt prompt) =>
        Json(writer =>
        {
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", prompt.Text);
            writer.WriteEndObject();
        });

    private static string Answering(string request, Decision decision, string input) =>
        Json(writer =>
        {
            writer.WriteString("type", "control_response");
            writer.WriteStartObject("response");
            writer.WriteString("subtype", "success");
            writer.WriteString("request_id", request);
            writer.WriteStartObject("response");
            if (decision is Deny)
            {
                writer.WriteString("behavior", "deny");
                writer.WriteString("message", "Denied in crafter.");
            }
            else
            {
                writer.WriteString("behavior", "allow");
                writer.WritePropertyName("updatedInput");
                writer.WriteRawValue(input);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });

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

    private static async Task<Decision?> Await(Task<Decision> decided, CancellationToken stop)
    {
        try
        {
            return await decided.WaitAsync(stop);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
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

    private static async Task<string?> Listen(Process process, CancellationToken stop)
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
