using System.Diagnostics;
using System.Runtime.CompilerServices;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCodeSession(string workingDirectory, string executable = "claude", string? resumeFrom = null) : IAgentSession
{
    private string? sessionId = resumeFrom;

    public string? SessionId => sessionId;

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        var process = ClaudeCodeProcess.Start(executable, workingDirectory, Arguments());
        if (process is null)
        {
            yield return new Failed($"Could not start '{executable}'. Is Claude Code installed and on the PATH?");

            yield break;
        }

        using (process)
        {
            var complaints = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await Tell(process, prompt);

            var transcript = new Transcript();
            var ended = false;
            var heardEverything = false;

            try
            {
                while (await Listen(process, stop) is { } line)
                {
                    var replies = transcript.Hear(line);
                    sessionId = transcript.SessionId ?? sessionId;

                    foreach (var reply in replies)
                    {
                        ended |= reply is Ending;

                        yield return reply;
                    }
                }

                heardEverything = !stop.IsCancellationRequested;
            }
            finally
            {
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

            await process.WaitForExitAsync(CancellationToken.None);
            var complaint = (await complaints).Trim();

            yield return new Failed(
                complaint.Length > 0 ? complaint : $"Claude Code exited with code {process.ExitCode} without an answer.");
        }
    }

    private string[] Arguments()
    {
        string[] always = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages", "--tools", ""];

        return sessionId is null ? always : [.. always, "--resume", sessionId];
    }

    private static async Task Tell(Process process, Prompt prompt)
    {
        try
        {
            await process.StandardInput.WriteAsync(prompt.Text);
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
