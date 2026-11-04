namespace Crafter.Presentation.Conversations.Messages;

// Splits an answer into prose and fenced code. A fence is three or more backticks or tildes and
// closes only on the same character, at least as many and nothing else, as markdown has it: so a
// block opened with ```` can hold ``` lines, which is how one shows markdown that has code in it.
public static class AnswerPieces
{
    public static IReadOnlyList<(string? Language, string Text)> Of(string answer)
    {
        List<(string? Language, string Text)> pieces = [];
        List<string> lines = [];
        string? language = null;
        Fence? open = null;

        foreach (var line in answer.Split('\n'))
        {
            var trimmed = line.Trim();

            if (open is null && Opening(trimmed) is { } fence)
            {
                Close(pieces, lines, null);
                open = fence;
                language = trimmed[fence.Length..].Trim().Split(' ', 2)[0];
            }
            else if (open is { } current && Closes(trimmed, current))
            {
                Close(pieces, lines, language);
                open = null;
                language = null;
            }
            else
            {
                lines.Add(line);
            }
        }

        Close(pieces, lines, open is null ? null : language);

        return pieces;
    }

    private static Fence? Opening(string line)
    {
        if (line.Length < 3 || line[0] is not ('`' or '~'))
        {
            return null;
        }

        var mark = line[0];
        var length = line.TakeWhile(each => each == mark).Count();
        if (length < 3)
        {
            return null;
        }

        // ```a`b``` is inline code on a line of its own, not a fence.
        return mark == '`' && line[length..].Contains('`') ? null : new Fence(mark, length);
    }

    private static bool Closes(string line, Fence fence) =>
        line.Length >= fence.Length && line.All(each => each == fence.Mark);

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

    private sealed record Fence(char Mark, int Length);
}
