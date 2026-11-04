namespace Crafter.Presentation.Conversations.Messages;

public enum SummaryChange
{
    Kept,
    Added,
    Removed,
}

// One line of a proposed summary beside the one it replaces: kept, added or taken out.
public sealed record SummaryLine(SummaryChange Change, string Text)
{
    public bool IsAdded => Change == SummaryChange.Added;

    public bool IsRemoved => Change == SummaryChange.Removed;

    public string Marked => Change switch
    {
        SummaryChange.Added => "+ ",
        SummaryChange.Removed => "- ",
        _ => "  ",
    } + Text;

    // The lines both keep, in order, found as their longest common run; the rest are taken out of
    // the current summary or added by the proposed one, removals first where they meet.
    public static IReadOnlyList<SummaryLine> Between(string? current, string proposed)
    {
        var before = Lines(current);
        var after = Lines(proposed);
        var common = new int[before.Length + 1, after.Length + 1];
        for (var i = before.Length - 1; i >= 0; i--)
        {
            for (var j = after.Length - 1; j >= 0; j--)
            {
                common[i, j] = before[i] == after[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        List<SummaryLine> lines = [];
        int x = 0, y = 0;
        while (x < before.Length || y < after.Length)
        {
            if (x < before.Length && y < after.Length && before[x] == after[y])
            {
                lines.Add(new SummaryLine(SummaryChange.Kept, before[x++]));
                y++;
            }
            else if (x < before.Length && (y == after.Length || common[x + 1, y] >= common[x, y + 1]))
            {
                lines.Add(new SummaryLine(SummaryChange.Removed, before[x++]));
            }
            else
            {
                lines.Add(new SummaryLine(SummaryChange.Added, after[y++]));
            }
        }

        return lines;
    }

    private static string[] Lines(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Trim().ReplaceLineEndings("\n").Split('\n');
}
