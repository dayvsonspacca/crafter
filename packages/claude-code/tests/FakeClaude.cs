namespace Crafter.ClaudeCode.Tests;

internal sealed class FakeClaude : IDisposable
{
    public const string Init = """echo '{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}'""";

    public const string Success = """echo '{"type":"result","subtype":"success","is_error":false,"stop_reason":"end_turn","session_id":"s-1"}'""";

    public FakeClaude(string script)
    {
        Folder = Directory.CreateTempSubdirectory("crafter-fake-claude-").FullName;
        Executable = Path.Combine(Folder, "claude");
        File.WriteAllText(Executable, $"#!/usr/bin/env bash\n{script}\n");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public string Folder { get; }

    public string Executable { get; }

    public static string Writes(string text) =>
        """printf '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"%s"}}}\n' """
        + $"\"{text}\"";

    public ClaudeCodeSession Session() => new(Folder, Executable);

    public void Dispose() => Directory.Delete(Folder, recursive: true);
}
