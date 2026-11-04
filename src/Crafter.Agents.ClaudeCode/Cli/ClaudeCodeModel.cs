namespace Crafter.Agents.ClaudeCode.Cli;

public sealed record ClaudeCodeModel(string Value, string? Name = null)
{
    public static string NameOf(string id)
    {
        var parts = id.Replace("claude-", string.Empty, StringComparison.Ordinal)
            .Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return id;
        }

        var family = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];

        return parts.Length == 1 ? family : $"{family} {string.Join('.', parts[1..])}";
    }
}
