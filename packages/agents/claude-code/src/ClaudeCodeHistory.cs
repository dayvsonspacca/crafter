using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode;

public sealed partial class ClaudeCodeHistory(string home)
{
    private const int Tail = 512 * 1024;

    private static readonly string[] Spoken = ["\"type\":\"user\"", "\"type\":\"assistant\""];

    public static string Home =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public Task<Recollection> List(Project project, int latest, CancellationToken stop = default) =>
        Task.Run(() => Recall(project, latest), stop);

    public Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default) =>
        Task.Run(() => Retell(Path.Combine(FolderOf(project), sessionId + ".jsonl"), sessionId), stop);

    private static PastChat? Summarize(FileInfo session)
    {
        if (First(session) is not { } first)
        {
            return null;
        }

        var last = Last(session);

        return new PastChat(
            Path.GetFileNameWithoutExtension(session.Name),
            ChatTitle.From(new Prompt(first)),
            session.LastWriteTimeUtc,
            last is not null && last != first ? new Prompt(last) : null);
    }

    private static string? First(FileInfo session) => Entries(Lines(session.FullName)).Select(Prompt).FirstOrDefault(prompt => prompt is not null);

    private static string? Last(FileInfo session) => Entries(TailLines(session.FullName).Reverse()).Select(Prompt).FirstOrDefault(prompt => prompt is not null);

    private static IReadOnlyList<Turn> Retell(string file, string sessionId)
    {
        List<Turn> turns = [];
        if (Path.GetFileName(sessionId) != sessionId)
        {
            return turns;
        }

        var answer = new StringBuilder();

        foreach (var entry in Entries(Lines(file)))
        {
            if (Prompt(entry) is { } prompt)
            {
                Close(turns, answer);
                turns.Add(new Asked(new Prompt(prompt)));
            }
            else if (turns.Count > 0 && Answer(entry) is { } text)
            {
                answer.Append(answer.Length > 0 ? "\n\n" : string.Empty).Append(text);
            }
        }

        Close(turns, answer);

        return turns;
    }

    private static void Close(List<Turn> turns, StringBuilder answer)
    {
        if (answer.Length > 0)
        {
            turns.Add(new Answered(answer.ToString(), new Finished()));
            answer.Clear();
        }
    }

    private static string? Prompt(JsonElement entry)
    {
        if (Text(entry, "type") != "user"
            || Flag(entry, "isMeta")
            || Flag(entry, "isSidechain")
            || Flag(entry, "isCompactSummary")
            || Flag(entry, "isVisibleInTranscriptOnly"))
        {
            return null;
        }

        var said = Message(entry) switch
        {
            { ValueKind: JsonValueKind.String } content => content.GetString(),
            { ValueKind: JsonValueKind.Array } blocks when !Blocks(blocks).Any(block => Text(block, "type") == "tool_result") => Written(blocks),
            _ => null,
        };

        return said?.Trim() is { Length: > 0 } prompt && !prompt.StartsWith('<') ? prompt : null;
    }

    private static string? Answer(JsonElement entry) =>
        Text(entry, "type") == "assistant" && !Flag(entry, "isSidechain") && Message(entry) is { ValueKind: JsonValueKind.Array } blocks
            ? Written(blocks)?.Trim() is { Length: > 0 } text ? text : null
            : null;

    private static string? Written(JsonElement blocks)
    {
        var texts = Blocks(blocks).Where(block => Text(block, "type") == "text").Select(block => Text(block, "text")).OfType<string>().ToList();

        return texts.Count > 0 ? string.Join("\n\n", texts) : null;
    }

    private static IEnumerable<JsonElement> Blocks(JsonElement blocks) => blocks.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object);

    private static JsonElement? Message(JsonElement entry) =>
        entry.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object && message.TryGetProperty("content", out var content)
            ? content
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static IEnumerable<JsonElement> Entries(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (Spoken.Any(spoken => line.Contains(spoken, StringComparison.Ordinal)) && Parse(line) is { } entry)
            {
                yield return entry;
            }
        }
    }

    private static IEnumerable<string> Lines(string file)
    {
        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(file);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var line in lines)
        {
            yield return line;
        }
    }

    private static IReadOnlyList<string> TailLines(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var start = Math.Max(0, stream.Length - Tail);
            stream.Seek(start, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split('\n');

            return start > 0 ? lines[1..] : lines;
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static JsonElement? Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);

            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Recollection Recall(Project project, int latest)
    {
        List<PastChat> chats = [];
        var total = 0;

        foreach (var session in Sessions(project))
        {
            if (chats.Count < latest && Summarize(session) is { } chat)
            {
                chats.Add(chat);
                total++;
            }
            else if (chats.Count >= latest && First(session) is not null)
            {
                total++;
            }
        }

        return new Recollection(chats, total);
    }

    private IEnumerable<FileInfo> Sessions(Project project)
    {
        var folder = new DirectoryInfo(FolderOf(project));

        return folder.Exists ? folder.EnumerateFiles("*.jsonl").OrderByDescending(file => file.LastWriteTimeUtc) : [];
    }

    private string FolderOf(Project project) => Path.Combine(home, "projects", Unsafe().Replace(project.Folder, "-"));

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex Unsafe();
}
