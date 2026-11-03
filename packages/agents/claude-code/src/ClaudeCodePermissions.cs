using System.Text.Json;
using static Crafter.Agents.ClaudeCode.JsonFields;

namespace Crafter.Agents.ClaudeCode;

internal static class ClaudeCodePermissions
{
    private const string Allows = "allow";

    public static string? Remembers(JsonElement suggestions)
    {
        if (suggestions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<(string Part, string Scope)> parts = [];

        foreach (var suggestion in Each(suggestions))
        {
            var scope = Scope(Text(suggestion, "destination"));
            parts.AddRange((Text(suggestion, "type") switch
            {
                "addRules" when Text(suggestion, "behavior") == Allows => Rules(suggestion),
                "addDirectories" => Strings(suggestion, "directories"),
                "setMode" when Text(suggestion, "mode") is { } mode => [$"{mode} mode"],
                _ => [],
            }).Select(part => (part, scope)));
        }

        var groups = parts.Distinct().GroupBy(each => each.Scope).Select(group => $"{string.Join(", ", group.Select(each => each.Part))} {group.Key}");

        return parts.Count > 0 ? string.Join(", ", groups) : null;
    }

    public static (IReadOnlyList<string> Rules, IReadOnlyList<string> Directories) Granted(string? suggestions)
    {
        if (suggestions is null)
        {
            return ([], []);
        }

        try
        {
            using var document = JsonDocument.Parse(suggestions);
            var each = Each(document.RootElement).ToList();

            return (
                [.. each.Where(suggestion => Text(suggestion, "type") == "addRules" && Text(suggestion, "behavior") == Allows).SelectMany(Rules)],
                [.. each.Where(suggestion => Text(suggestion, "type") == "addDirectories").SelectMany(suggestion => Strings(suggestion, "directories"))]);
        }
        catch (JsonException)
        {
            return ([], []);
        }
    }

    private static string Scope(string? destination) => destination switch
    {
        "userSettings" => "everywhere",
        "projectSettings" or "localSettings" => "in this project",
        _ => "in this chat",
    };

    private static IEnumerable<string> Rules(JsonElement suggestion) =>
        suggestion.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array
            ? Each(rules)
                .Where(rule => Text(rule, "toolName") is not null)
                .Select(rule => Text(rule, "ruleContent") is { Length: > 0 } content ? $"{Text(rule, "toolName")}({content})" : Text(rule, "toolName")!)
            : [];

    private static IEnumerable<string> Strings(JsonElement holder, string property) =>
        holder.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!)
            : [];

    private static IEnumerable<JsonElement> Each(JsonElement array) =>
        array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object) : [];
}
