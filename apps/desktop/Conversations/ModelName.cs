namespace Crafter.Desktop.Conversations;

public sealed record ModelName(string Id)
{
    public override string ToString()
    {
        var parts = Id.Replace("claude-", string.Empty, StringComparison.Ordinal)
            .Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return Id;
        }

        var family = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];

        return parts.Length == 1 ? family : $"{family} {string.Join('.', parts[1..])}";
    }
}
