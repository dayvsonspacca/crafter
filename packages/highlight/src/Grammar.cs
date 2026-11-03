namespace Crafter.Highlight;

public sealed record Grammar
{
    public required IReadOnlySet<string> Keywords { get; init; }

    public IReadOnlyList<string> LineComments { get; init; } = [];

    public IReadOnlyList<(string Open, string Close)> BlockComments { get; init; } = [];

    public string Quotes { get; init; } = "\"'";

    public string StringPrefixes { get; init; } = string.Empty;

    public bool KnowsTypes { get; init; } = true;

    public bool BracketAttributes { get; init; }

    public bool AtAttributes { get; init; }

    public bool IgnoresCase { get; init; }

    public bool IsKeyword(string word) => Keywords.Contains(IgnoresCase ? word.ToLowerInvariant() : word);
}
