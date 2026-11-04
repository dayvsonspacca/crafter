namespace Crafter.Agents.ClaudeCode.Cli;

// How a session learns what the project is: the system prompt carries the summary the user wrote,
// ahead of the skills and the axioms, as context rather than rules.
public static class ClaudeCodeSummary
{
    public static string Brief(string summary) => $"""
        # Project summary

        The user wrote this summary of the project in Crafter, the app this session runs in. Take it as what you know about the project before you start: it tells what the project is, not rules to follow. Claude Code's CLAUDE.md and AGENTS.md files are turned off: this summary takes their place.

        {summary.Trim()}

        """;
}
