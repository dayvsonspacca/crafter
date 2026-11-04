namespace Crafter.Agents.ClaudeCode.Cli;

// Claude Code writes what a background job prints to a file of its own, which may grow as it is read.
public static class ClaudeCodeOutput
{
    private const int Tail = 16 * 1024;

    public static string? Latest(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = Math.Max(0, stream.Length - Tail);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            // Reading from the middle of the file starts in the middle of a line.
            return start > 0 && text.IndexOf('\n') is var cut and >= 0 ? text[(cut + 1)..] : text;
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
