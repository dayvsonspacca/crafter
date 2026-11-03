namespace Crafter.Agents.ClaudeCode;

public sealed record ClaudeCodeOptions(string Executable, string? Model = null, string? Effort = null, string? Tools = null)
{
    public const string Chats = "chat";

    public const string Edits = "acceptEdits";

    private const string Legacy = "edit";

    public IReadOnlyList<string> Arguments(string? resumeFrom)
    {
        List<string> arguments =
        [
            "-p",
            "--input-format", "stream-json",
            "--output-format", "stream-json",
            "--verbose",
            "--include-partial-messages",
            "--permission-prompt-tool", "stdio",
        ];

        arguments.AddRange(Tools switch
        {
            Chats => ["--tools", string.Empty],
            null or Legacy => ["--permission-mode", Edits],
            var mode => ["--permission-mode", mode],
        });

        if (Model is { } model)
        {
            arguments.AddRange(["--model", model]);
        }

        if (Effort is { } effort)
        {
            arguments.AddRange(["--effort", effort]);
        }

        if (resumeFrom is { } session)
        {
            arguments.AddRange(["--resume", session]);
        }

        return arguments;
    }
}
