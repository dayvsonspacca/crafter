namespace Crafter.Core.Folders;

public static class FuzzyMatch
{
    public static IReadOnlyList<int>? Hits(string text, string wanted)
    {
        var hits = new List<int>(wanted.Length);
        var from = 0;

        foreach (var letter in wanted)
        {
            var at = text.IndexOf(letter.ToString(), from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return null;
            }

            hits.Add(at);
            from = at + 1;
        }

        return hits;
    }
}
