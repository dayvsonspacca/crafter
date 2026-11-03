using System.Text.Json;
using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode;

public sealed class Transcript(string? folder = null)
{
    private readonly Dictionary<string, string> inputs = [];

    public string? SessionId { get; private set; }

    public string InputOf(string request) => inputs.GetValueOrDefault(request) ?? "{}";

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
                "assistant" => Actions(root),
                "user" => Results(root),
                "control_request" => Approval(root),
                "result" => [Conclusion(root)],
                _ => [],
            };
        }
    }

    private static IReadOnlyList<Reply> Introduction(JsonElement root) =>
        Text(root, "model") is { } model ? [new Began(ModelName.Of(model))] : [];

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

    private IReadOnlyList<Reply> Actions(JsonElement root) =>
        [
            .. Blocks(root)
                .Where(block => Text(block, "type") == "tool_use" && Text(block, "id") is not null && Text(block, "name") is not null)
                .Select(block => Acting(Text(block, "id")!, Text(block, "name")!, Input(block))),
        ];

    private static IReadOnlyList<Reply> Results(JsonElement root) =>
        [
            .. Blocks(root)
                .Where(block => Text(block, "type") == "tool_result" && Text(block, "tool_use_id") is not null)
                .Select(block => new Acted(
                    Text(block, "tool_use_id")!,
                    block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True,
                    Said(block))),
        ];

    private IReadOnlyList<Reply> Approval(JsonElement root)
    {
        if (Text(root, "request_id") is not { } request
            || !root.TryGetProperty("request", out var asked)
            || Text(asked, "subtype") != "can_use_tool"
            || Text(asked, "tool_name") is not { } tool)
        {
            return [];
        }

        var input = Input(asked);
        inputs[request] = input.ValueKind == JsonValueKind.Object ? input.GetRawText() : "{}";

        return [new NeedsApproval(request, Text(asked, "display_name") ?? tool, ClaudeCodeTools.Describe(tool, input, folder).Summary, Text(asked, "description"))];
    }

    private Acting Acting(string id, string tool, JsonElement input)
    {
        var (summary, stat) = ClaudeCodeTools.Describe(tool, input, folder);

        return new Acting(id, tool, summary, stat);
    }

    private static JsonElement Input(JsonElement holder) =>
        holder.TryGetProperty("input", out var input) ? input : default;

    private static IEnumerable<JsonElement> Blocks(JsonElement root) =>
        root.TryGetProperty("message", out var message)
        && message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty("content", out var content)
        && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object)
            : [];

    private static string Said(JsonElement result) =>
        result.TryGetProperty("content", out var content)
            ? content.ValueKind switch
            {
                JsonValueKind.String => content.GetString()!,
                JsonValueKind.Array => string.Join('\n', content.EnumerateArray().Select(part => part.ValueKind == JsonValueKind.Object ? Text(part, "text") : null).OfType<string>()),
                _ => string.Empty,
            }
            : string.Empty;

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
