using System.Text.Json;
using System.Text.RegularExpressions;
using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Core.Agents.Background;
using Crafter.Core.Conversations.Replies;
using static Crafter.Agents.ClaudeCode.Protocol.JsonFields;

namespace Crafter.Agents.ClaudeCode.Protocol;

public sealed partial class ClaudeCodeTranscript(string? folder = null)
{
    private const string AsksTheUser = "AskUserQuestion";

    private const string Watches = "Monitor";

    private static readonly string[] Over = ["completed", "failed", "killed", "stopped"];

    private readonly Dictionary<string, Request> requests = [];

    private readonly Dictionary<string, string> tools = [];

    private readonly Dictionary<string, string> outputs = [];

    private readonly Dictionary<string, string> owners = [];

    private readonly List<Job> jobs = [];

    public string? SessionId { get; private set; }

    public IReadOnlyList<Job> Jobs => jobs;

    public bool Opened { get; private set; }

    public string? Started { get; private set; }

    public bool JobsMoved { get; private set; }

    public string InputOf(string request) => requests.GetValueOrDefault(request)?.Input ?? "{}";

    public string? OutputOf(string job) => owners.GetValueOrDefault(job) is { } owner ? outputs.GetValueOrDefault(owner) : null;

    public string? SuggestionsOf(string request) => requests.GetValueOrDefault(request)?.Suggestions;

    public static string? Withdraws(string line)
    {
        if (!line.Contains("\"control_cancel_request\"", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object && Text(root, "type") == "control_cancel_request" ? Text(root, "request_id") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

        Opened = false;
        Started = null;
        JobsMoved = false;

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
                "system" => Background(root),
                "command_lifecycle" => Lifecycle(root),
                "stream_event" => Fragment(root),
                "assistant" => Actions(root),
                "user" => Results(root),
                "control_request" => Approval(root),
                "control_cancel_request" when Text(root, "request_id") is { } request => [new Withdrawn(request)],
                "result" => [Conclusion(root)],
                _ => [],
            };
        }
    }

    private IReadOnlyList<Reply> Introduction(JsonElement root)
    {
        Opened = true;

        return Text(root, "model") is { } model ? [new Began(ClaudeCodeModel.NameOf(model))] : [];
    }

    private IReadOnlyList<Reply> Lifecycle(JsonElement root)
    {
        if (Text(root, "state") == "started")
        {
            Started = Text(root, "command_uuid");
        }

        return [];
    }

    private IReadOnlyList<Reply> Background(JsonElement root)
    {
        switch (Text(root, "subtype"))
        {
            case "task_started" when Text(root, "task_id") is { } id:
                Track(id, Text(root, "task_type"), Text(root, "tool_use_id"), Text(root, "description"));
                break;
            case "task_updated" when Text(root, "task_id") is { } id && root.TryGetProperty("patch", out var patch) && Over.Contains(Text(patch, "status")):
                Forget(id);
                break;
            case "task_notification" when Text(root, "task_id") is { } id && Over.Contains(Text(root, "status")):
                Forget(id);
                break;
            case "background_tasks_changed" when root.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array:
                var listed = tasks.EnumerateArray().Where(task => Text(task, "task_id") is not null).ToList();
                foreach (var gone in jobs.Where(job => listed.All(task => Text(task, "task_id") != job.Id)).ToList())
                {
                    Forget(gone.Id);
                }

                foreach (var task in listed)
                {
                    Track(Text(task, "task_id")!, Text(task, "task_type"), Text(task, "tool_use_id"), Text(task, "description"));
                }

                break;
        }

        return [];
    }

    private void Track(string id, string? type, string? tool, string? description)
    {
        var at = jobs.FindIndex(job => job.Id == id);
        if (tool is not null)
        {
            owners[id] = tool;
        }

        var owner = owners.GetValueOrDefault(id);
        var kind = type switch
        {
            "local_bash" when owner is not null && tools.GetValueOrDefault(owner) == Watches => JobKind.Monitor,
            "local_bash" => JobKind.Shell,
            null when at >= 0 => jobs[at].Kind,
            _ => JobKind.Agent,
        };
        var job = new Job(
            id,
            kind,
            description is { Length: > 0 } said ? said : at >= 0 ? jobs[at].Description : id,
            OutputOf(id) is not null);

        if (at < 0)
        {
            jobs.Add(job);
            JobsMoved = true;
        }
        else if (jobs[at] != job)
        {
            jobs[at] = job;
            JobsMoved = true;
        }
    }

    private void Forget(string id) => JobsMoved |= jobs.RemoveAll(job => job.Id == id) > 0;

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

    private IReadOnlyList<Reply> Results(JsonElement root) =>
        [
            .. Blocks(root)
                .Where(block => Text(block, "type") == "tool_result" && Text(block, "tool_use_id") is not null)
                .Select(block => Result(Text(block, "tool_use_id")!, block)),
        ];

    private Acted Result(string tool, JsonElement block)
    {
        var said = Said(block);
        if (Written().Match(said) is { Success: true } written)
        {
            outputs[tool] = written.Groups[1].Value;
            foreach (var (job, owner) in owners.Where(each => each.Value == tool && jobs.Any(listed => listed.Id == each.Key)).ToList())
            {
                Track(job, null, owner, null);
            }
        }

        return new Acted(tool, block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True, said);
    }

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
        var suggestions = asked.TryGetProperty("permission_suggestions", out var offered) && offered.ValueKind == JsonValueKind.Array && offered.GetArrayLength() > 0
            ? offered
            : default;
        requests[request] = new Request(
            input.ValueKind == JsonValueKind.Object ? input.GetRawText() : "{}",
            suggestions.ValueKind == JsonValueKind.Array ? suggestions.GetRawText() : null);

        if (tool == AsksTheUser && Questions(input) is { Count: > 0 } questions)
        {
            return [new NeedsAnswers(request, questions)];
        }

        var (shown, summary, _) = ClaudeCodeTools.Shown(tool, input, folder);

        return
        [
            new NeedsApproval(
                request,
                Text(asked, "display_name") is { } named ? shown with { Name = named } : shown,
                summary,
                Text(asked, "description"),
                ClaudeCodePermissions.Remembers(suggestions)),
        ];
    }

    private static IReadOnlyList<Question> Questions(JsonElement input) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty("questions", out var questions) && questions.ValueKind == JsonValueKind.Array
            ? [.. questions.EnumerateArray().Select(Posed).OfType<Question>()]
            : [];

    private static Question? Posed(JsonElement question)
    {
        if (question.ValueKind != JsonValueKind.Object
            || Text(question, "question") is not { } text
            || !question.TryGetProperty("options", out var options)
            || options.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        IReadOnlyList<Pick> picks =
        [
            .. options.EnumerateArray()
                .Where(option => option.ValueKind == JsonValueKind.Object && Text(option, "label") is { Length: > 0 })
                .Select(option => new Pick(Text(option, "label")!, Text(option, "description") ?? string.Empty)),
        ];

        return new Question(
            text,
            Text(question, "header") ?? string.Empty,
            question.TryGetProperty("multiSelect", out var several) && several.ValueKind == JsonValueKind.True,
            picks);
    }

    private Acting Acting(string id, string tool, JsonElement input)
    {
        tools[id] = tool;
        var (shown, summary, changes) = ClaudeCodeTools.Shown(tool, input, folder);

        return new Acting(id, shown, summary, changes);
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
    [GeneratedRegex(@"written to: (\S+\.output)")]
    private static partial Regex Written();

    private sealed record Request(string Input, string? Suggestions);
}
