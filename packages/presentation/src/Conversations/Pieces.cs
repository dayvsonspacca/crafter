namespace Crafter.Presentation.Conversations;

public static class Pieces
{
    public static IReadOnlyList<(string? Language, string Text)> Of(string answer)
    {
        List<(string? Language, string Text)> pieces = [];
        List<string> lines = [];
        string? language = null;
        string? fence = null;

        foreach (var line in answer.Split('\n'))
        {
            var trimmed = line.Trim();

            if (fence is null && (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)))
            {
                Close(pieces, lines, null);
                fence = trimmed[..3];
                language = trimmed[3..].Trim().Split(' ', 2)[0];
            }
            else if (fence is not null && trimmed == fence)
            {
                Close(pieces, lines, language);
                fence = null;
                language = null;
            }
            else
            {
                lines.Add(line);
            }
        }

        Close(pieces, lines, fence is null ? null : language);

        return pieces;
    }

    private static void Close(List<(string? Language, string Text)> pieces, List<string> lines, string? language)
    {
        var text = string.Join('\n', lines);
        lines.Clear();

        if (language is not null)
        {
            pieces.Add((language, text.TrimEnd()));
        }
        else if (text.Trim().Length > 0)
        {
            pieces.Add((null, text.Trim('\n', '\r')));
        }
    }
}
