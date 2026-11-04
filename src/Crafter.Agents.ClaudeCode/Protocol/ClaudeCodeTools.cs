using System.Text.Json;
using Crafter.Core.Conversations.Replies;
using static Crafter.Agents.ClaudeCode.Protocol.JsonFields;

namespace Crafter.Agents.ClaudeCode.Protocol;

public static class ClaudeCodeTools
{
    public static (string Summary, Changes? Changes) Describe(string tool, JsonElement input, string? folder)
    {
        string? Field(string name) => Text(input, name);

        string File() => Relative(Field("file_path") ?? Field("notebook_path") ?? Field("path") ?? string.Empty, folder);

        return tool switch
        {
            "Bash" => (FirstLine(Field("command") ?? string.Empty), null),
            "Read" => (File(), null),
            "Edit" => (File(), new Changes(Lines(Field("new_string")), Lines(Field("old_string")))),
            "MultiEdit" => (File(), new Changes(Edits(input, "new_string"), Edits(input, "old_string"))),
            "Write" => (File(), new Changes(Lines(Field("content")))),
            "NotebookEdit" => (File(), null),
            "Grep" or "Glob" => (Field("pattern") ?? string.Empty, null),
            "WebFetch" => (Field("url") ?? string.Empty, null),
            "WebSearch" => (Field("query") ?? string.Empty, null),
            "Task" or "Agent" => (Field("description") ?? string.Empty, null),
            _ => (Field("description") ?? Field("command") ?? Field("file_path") ?? tool, null),
        };
    }

    // The name a tool goes by in the chat. A tool server's tool, mcp__<server>__<tool>, goes by its
    // server, and what it did reads as words with its first argument: "crafter", "set project icon · …".
    public static (string Tool, string Summary, Changes? Changes) Shown(string tool, JsonElement input, string? folder)
    {
        if (Served(tool) is not { } served)
        {
            var (summary, changes) = Describe(tool, input, folder);

            return (tool, summary, changes);
        }

        var action = served.Tool.Replace('_', ' ').Replace('-', ' ').Trim();
        var argument = FirstArgument(input) is { } first ? Relative(FirstLine(first), folder) : null;

        return (served.Server, argument is { Length: > 0 } ? $"{action} · {argument}" : action, null);
    }

    private static (string Server, string Tool)? Served(string tool)
    {
        if (!tool.StartsWith("mcp__", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = tool["mcp__".Length..].Split("__", 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return null;
        }

        // Connectors from claude.ai arrive as claude_ai_<Name>; the name is enough.
        var server = parts[0].StartsWith("claude_ai_", StringComparison.Ordinal) ? parts[0]["claude_ai_".Length..] : parts[0];

        return (server.Replace('_', ' '), parts[1]);
    }

    private static string? FirstArgument(JsonElement input) =>
        input.ValueKind == JsonValueKind.Object
            ? input.EnumerateObject()
                .Where(field => field.Value.ValueKind == JsonValueKind.String)
                .Select(field => field.Value.GetString()!.Trim())
                .FirstOrDefault(text => text.Length > 0)
            : null;

    private static string Relative(string path, string? folder) =>
        folder is not null && path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path[(folder.Length + 1)..] : path;

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();

    private static int Lines(string? text) => string.IsNullOrEmpty(text) ? 0 : text.TrimEnd('\n').Split('\n').Length;

    private static int Edits(JsonElement input, string field) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array
            ? edits.EnumerateArray().Sum(edit => edit.ValueKind == JsonValueKind.Object && edit.TryGetProperty(field, out var text) && text.ValueKind == JsonValueKind.String ? Lines(text.GetString()) : 0)
            : 0;
}
