using System.Text.Json;
using Crafter.Core.Conversations;

namespace Crafter.ClaudeCode;

public sealed class Transcript
{
    public string? SessionId { get; private set; }

    public IReadOnlyList<Reply> Hear(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            SessionId = Text(root, "session_id") ?? SessionId;

            return Text(root, "type") switch
            {
                "system" when Text(root, "subtype") == "init" => Introduction(root),
                "stream_event" => Fragment(root),
                "result" => [Conclusion(root)],
                _ => [],
            };
        }
    }

    private static IReadOnlyList<Reply> Introduction(JsonElement root) =>
        Text(root, "model") is { } model ? [new Began(model)] : [];

    private static IReadOnlyList<Reply> Fragment(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var streamed)
            || Text(streamed, "type") != "content_block_delta"
            || !streamed.TryGetProperty("delta", out var delta)
            || Text(delta, "type") != "text_delta")
        {
            return [];
        }

        return Text(delta, "text") is { Length: > 0 } text ? [new Wrote(text)] : [];
    }

    private static Ending Conclusion(JsonElement root)
    {
        if (root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True)
        {
            return new Failed(Text(root, "result") ?? Text(root, "subtype") ?? "Claude Code failed without saying why.");
        }

        return Text(root, "stop_reason") switch
        {
            "max_tokens" => new CutShort(),
            "refusal" => new Refused(Category(root)),
            _ => new Finished(),
        };
    }

    private static string? Category(JsonElement root) =>
        root.TryGetProperty("stop_details", out var details) && details.ValueKind == JsonValueKind.Object
            ? Text(details, "category")
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
