namespace Crafter.Desktop.Conversations;

// Some drags carry files only as text: file:// URIs or absolute paths, one per line, the
// way XWayland hands over a drag from a Wayland file manager.
public static class DroppedText
{
    // The paths the text names, when every line is a file:// URI or an absolute path that
    // exists; otherwise none, and the text is just text.
    public static IReadOnlyList<string> Paths(string text, Func<string, bool> exists)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> paths = [];

        foreach (var line in lines.Where(line => !line.StartsWith('#')))
        {
            var path = line.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.IsFile
                ? uri.LocalPath
                : line;

            if (!Path.IsPathFullyQualified(path) || !exists(path))
            {
                return [];
            }

            paths.Add(path);
        }

        return paths;
    }

    public static IReadOnlyList<string> Paths(string text) => Paths(text, path => File.Exists(path) || Directory.Exists(path));

    // Hyprland can hand an XWayland window the clipboard instead of what was dragged, so a
    // copied picture arrives as its raw bytes read as text. Text with nulls, control characters
    // or undecodable bytes (U+FFFD) is not something anyone meant to type.
    public static bool IsReadable(string text) =>
        !text.Any(letter => letter == '\uFFFD' || (char.IsControl(letter) && letter is not ('\t' or '\r' or '\n')));
}
