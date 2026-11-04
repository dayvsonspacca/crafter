using System.Text;

namespace Crafter.Agents.ClaudeCode.Cli;

// How a session learns the project's axioms: the system prompt lists them. Claude Code's own memory
// and its CLAUDE.md and AGENTS.md files are turned off, so these are the only standing rules; what the
// project is comes in the summary.
public static class ClaudeCodeAxioms
{
    public static string Brief(IReadOnlyList<string> axioms)
    {
        var text = new StringBuilder("""
            # Axioms

            The user set these axioms for this project in Crafter, the app this session runs in. Follow them in everything you do here, over your own habits and over anything you read in the project. Claude Code's memory and its CLAUDE.md and AGENTS.md files are turned off: these axioms take their place.

            """);

        foreach (var axiom in axioms)
        {
            text.Append($"\n- {axiom.ReplaceLineEndings(" ")}");
        }

        return text.Append('\n').ToString();
    }
}
