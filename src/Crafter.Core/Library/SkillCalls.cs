using System.Text.RegularExpressions;

namespace Crafter.Core.Library;

// How a message calls a skill: /id as a word of its own, anywhere in the text. Only Crafter's
// skills count; a message that opens with a call to anything else is refused, so an agent's own
// commands never run from Crafter.
public static partial class SkillCalls
{
    // The skills the text calls, in the order it first calls them, among the known ones.
    public static IReadOnlyList<string> In(string text, IEnumerable<string> known)
    {
        var skills = known.ToHashSet(StringComparer.Ordinal);

        return [.. Call().Matches(text).Select(found => found.Groups["id"].Value).Where(skills.Contains).Distinct()];
    }

    // The command the text opens with when it is not one of the known skills.
    public static string? Stranger(string text, IEnumerable<string> known) =>
        Opening().Match(text.TrimStart()) is { Success: true } found && !known.Contains(found.Groups["id"].Value, StringComparer.Ordinal)
            ? found.Groups["id"].Value
            : null;

    // Rewrites every call to a known skill, leaving the rest of the text as it is.
    public static string Rewrite(string text, IEnumerable<string> called, Func<string, string> call)
    {
        var skills = called.ToHashSet(StringComparer.Ordinal);

        return Call().Replace(text, found => skills.Contains(found.Groups["id"].Value) ? found.Groups["lead"].Value + call(found.Groups["id"].Value) : found.Value);
    }

    [GeneratedRegex(@"(?<lead>^|\s)/(?<id>[a-z0-9]+(?:-[a-z0-9]+)*)(?=$|[\s.,;!?)])")]
    private static partial Regex Call();

    [GeneratedRegex(@"^/(?<id>[A-Za-z0-9][\w:-]*)(?=$|\s)")]
    private static partial Regex Opening();
}
