namespace Crafter.Agents.ClaudeCode;

internal static class ClaudeCodeLabels
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [ClaudeCodeOptions.Edits] = "Edit files",
        ["plan"] = "Plan",
        ["auto"] = "Auto",
        ["dontAsk"] = "Don't ask",
        ["bypassPermissions"] = "Bypass permissions",
        ["manual"] = "Manual",
        ["xhigh"] = "Extra high",
    };

    public static string Of(string value) =>
        Names.TryGetValue(value, out var name) ? name : Humanize(value);

    private static string Humanize(string value)
    {
        var words = string.Concat(value.Select((letter, at) => at > 0 && char.IsUpper(letter) ? " " + char.ToLowerInvariant(letter) : letter.ToString()))
            .Replace('-', ' ')
            .Replace('_', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}
