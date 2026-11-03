using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Crafter.Core.Conversations;

namespace Crafter.ClaudeCode;

public sealed class ClaudeCodeSession(string workingDirectory, string executable = "claude") : IAgent
{
    private static readonly string[] InheritedSession =
    [
        "CLAUDECODE",
        "CLAUDE_CODE_ENTRYPOINT",
        "CLAUDE_CODE_SESSION_ID",
        "CLAUDE_CODE_CHILD_SESSION",
        "CLAUDE_CODE_SESSION_ATTENDED",
        "CLAUDE_CODE_MESSAGING_SOCKET",
        "CLAUDE_CODE_MESSAGING_TOKEN",
        "CLAUDE_CODE_EXECPATH",
        "CLAUDE_PID",
    ];

    private string? sessionId;

    public async IAsyncEnumerable<Reply> Answer(Prompt prompt, [EnumeratorCancellation] CancellationToken stop = default)
    {
        var process = Launch();
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
                    foreach (var reply in transcript.Hear(line))
                    {
                        ended |= reply is Ending;

                        yield return reply;
                    }
                }

                heardEverything = !stop.IsCancellationRequested;
            }
            finally
            {
                sessionId = transcript.SessionId ?? sessionId;
                if (!heardEverything)
                {
                    Kill(process);
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

    private Process? Launch()
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in Arguments())
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var variable in InheritedSession)
        {
            start.Environment.Remove(variable);
        }

        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;
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

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
