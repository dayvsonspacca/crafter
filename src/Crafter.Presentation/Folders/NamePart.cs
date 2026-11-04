namespace Crafter.Presentation.Folders;

public sealed record NamePart(string Text, bool IsHit)
{
    public static IReadOnlyList<NamePart> Split(string text, IEnumerable<int> hits)
    {
        var hit = hits.ToHashSet();
        var pieces = new List<NamePart>();
        var start = 0;

        for (var at = 1; at <= text.Length; at++)
        {
            if (at == text.Length || hit.Contains(at) != hit.Contains(start))
            {
                pieces.Add(new NamePart(text[start..at], hit.Contains(start)));
                start = at;
            }
        }

        return pieces;
    }
}
