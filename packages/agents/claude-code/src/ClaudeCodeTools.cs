using System.Text.Json;
using static Crafter.Agents.ClaudeCode.JsonFields;

namespace Crafter.Agents.ClaudeCode;

public static class ClaudeCodeTools
{
    public static (string Summary, string? Stat) Describe(string tool, JsonElement input, string? folder)
    {
        string? Field(string name) => Text(input, name);

        string File() => Relative(Field("file_path") ?? Field("notebook_path") ?? Field("path") ?? string.Empty, folder);

        return tool switch
        {
            "Bash" => (FirstLine(Field("command") ?? string.Empty), null),
            "Read" => (File(), null),
            "Edit" => (File(), Changes(Lines(Field("new_string")), Lines(Field("old_string")))),
            "MultiEdit" => (File(), Changes(Edits(input, "new_string"), Edits(input, "old_string"))),
            "Write" => (File(), $"+{Lines(Field("content"))}"),
            "NotebookEdit" => (File(), null),
            "Grep" or "Glob" => (Field("pattern") ?? string.Empty, null),
            "WebFetch" => (Field("url") ?? string.Empty, null),
            "WebSearch" => (Field("query") ?? string.Empty, null),
            "Task" or "Agent" => (Field("description") ?? string.Empty, null),
            _ => (Field("description") ?? Field("command") ?? Field("file_path") ?? tool, null),
        };
    }

    private static string Relative(string path, string? folder) =>
        folder is not null && path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path[(folder.Length + 1)..] : path;

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();

    private static int Lines(string? text) => string.IsNullOrEmpty(text) ? 0 : text.TrimEnd('\n').Split('\n').Length;

    private static int Edits(JsonElement input, string field) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array
            ? edits.EnumerateArray().Sum(edit => edit.ValueKind == JsonValueKind.Object && edit.TryGetProperty(field, out var text) && text.ValueKind == JsonValueKind.String ? Lines(text.GetString()) : 0)
            : 0;

    private static string Changes(int added, int removed) => $"+{added} −{removed}";
}
