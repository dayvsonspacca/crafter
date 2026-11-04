using System.ComponentModel;
using System.Diagnostics;

namespace Crafter.Agents.ClaudeCode.Cli;

internal static class ClaudeCodeProcess
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

    public static Process? Start(string executable, string workingDirectory, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var variable in InheritedSession)
        {
            start.Environment.Remove(variable);
        }

        // Crafter's summary and axioms stand in for Claude Code's memory and its CLAUDE.md and AGENTS.md files,
        // so those stay off, for the sessions Crafter starts only.
        start.Environment["CLAUDE_CODE_DISABLE_CLAUDE_MDS"] = "1";
        start.Environment["CLAUDE_CODE_DISABLE_AUTO_MEMORY"] = "1";

        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    public static void Kill(Process process)
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
