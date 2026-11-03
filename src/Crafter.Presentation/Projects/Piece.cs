namespace Crafter.Presentation.Projects;

public sealed record Piece(string Text, bool IsHit)
{
    public static IReadOnlyList<Piece> Split(string text, IEnumerable<int> hits)
    {
        var hit = hits.ToHashSet();
        var pieces = new List<Piece>();
        var start = 0;

        for (var at = 1; at <= text.Length; at++)
        {
            if (at == text.Length || hit.Contains(at) != hit.Contains(start))
            {
                pieces.Add(new Piece(text[start..at], hit.Contains(start)));
                start = at;
            }
        }

        return pieces;
    }
}
