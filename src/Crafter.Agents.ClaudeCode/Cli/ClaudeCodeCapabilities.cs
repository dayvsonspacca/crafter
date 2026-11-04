using System.Text.Json;
using System.Text.RegularExpressions;

namespace Crafter.Agents.ClaudeCode.Cli;

public sealed partial record ClaudeCodeCapabilities(IReadOnlyList<ClaudeCodeModel> Models, IReadOnlyList<string> Efforts, IReadOnlyList<string> Modes)
{
    private const string Recommended = "default";

    public static readonly ClaudeCodeCapabilities Unknown = new([], [], []);

    public static ClaudeCodeCapabilities Parse(string help)
    {
        var flat = Spaces().Replace(help, " ");

        return new ClaudeCodeCapabilities(
            [.. Listed(Model().Match(flat), Quoted()).Select(alias => new ClaudeCodeModel(alias))],
            Listed(Effort().Match(flat), Word()),
            Listed(Mode().Match(flat), Quoted()));
    }

    public ClaudeCodeCapabilities Introduced(string? answer) => Offered(answer) is { Count: > 0 } models ? this with { Models = models } : this;

    private static IReadOnlyList<ClaudeCodeModel> Offered(string? answer)
    {
        if (answer is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(answer);
            return document.RootElement.TryGetProperty("response", out var outer)
                && outer.TryGetProperty("response", out var inner)
                && inner.TryGetProperty("models", out var models)
                && models.ValueKind == JsonValueKind.Array
                    ? [.. models.EnumerateArray().Select(Model).OfType<ClaudeCodeModel>().Where(model => model.Value != Recommended)]
                    : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ClaudeCodeModel? Model(JsonElement model) =>
        model.ValueKind == JsonValueKind.Object && model.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String
            ? new ClaudeCodeModel(
                value.GetString()!,
                model.TryGetProperty("displayName", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null)
            : null;

    private static IReadOnlyList<string> Listed(Match option, Regex value) =>
        option.Success ? [.. value.Matches(option.Groups[1].Value).Select(found => found.Groups[1].Value).Distinct()] : [];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"--model <\w+> .*?\(e\.g\. (.*?)\)")]
    private static partial Regex Model();

    [GeneratedRegex(@"--effort <\w+> .*?\(([^)]*)\)")]
    private static partial Regex Effort();

    [GeneratedRegex(@"--permission-mode <\w+> .*?\(choices: ([^)]*)\)")]
    private static partial Regex Mode();

    [GeneratedRegex(@"['""]([\w.-]+)['""]")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"([\w.-]+)")]
    private static partial Regex Word();
}
