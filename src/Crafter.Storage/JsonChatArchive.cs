using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;

namespace Crafter.Storage;

public sealed class JsonChatArchive(string folder) : IChatArchive
{
    public IReadOnlyList<ChatRecord> Load(Project project)
    {
        var file = FileOf(project);
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize(File.ReadAllText(file), ChatJson.Default.ListChatEntry) ?? [];

            return [.. entries.Select(Record)];
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            return [];
        }
    }

    public void Save(Project project, IReadOnlyList<ChatRecord> chats)
    {
        Directory.CreateDirectory(folder);

        List<ChatEntry> entries = [.. chats.Select(Entry)];
        File.WriteAllText(FileOf(project), JsonSerializer.Serialize(entries, ChatJson.Default.ListChatEntry));
    }

    private string FileOf(Project project)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(project.Folder)))[..12];

        return Path.Combine(folder, $"{Path.GetFileName(project.Folder)}-{hash}.json");
    }

    private static ChatRecord Record(ChatEntry entry) =>
        new(
            new ChatId(entry.Id),
            new AgentId(entry.Agent),
            ChatTitle.Of(entry.Title),
            entry.SessionId,
            [.. entry.Turns.Select(Turn)])
        {
            Adjustments = new Configuration(entry.Adjustments ?? []),
        };

    private static ChatEntry Entry(ChatRecord record) =>
        new(record.Id.Value, record.Agent.Value, record.Title.Text, record.SessionId, [.. record.Turns.Select(Entry)])
        {
            Adjustments = record.Adjustments.Values.Count > 0 ? new Dictionary<string, string>(record.Adjustments.Values) : null,
        };

    private static Turn Turn(TurnEntry entry) => entry.Kind switch
    {
        "asked" => new Asked(new Prompt(entry.Text)),
        _ => new Answered(entry.Text, Ending(entry)),
    };

    private static Ending Ending(TurnEntry entry) => entry.Ending switch
    {
        "finished" => new Finished(),
        "stopped" => new Stopped(),
        "cutShort" => new CutShort(),
        "refused" => new Refused(entry.Detail),
        _ => new Failed(entry.Detail ?? "The answer could not be read back."),
    };

    private static TurnEntry Entry(Turn turn) => turn switch
    {
        Asked asked => new TurnEntry("asked", asked.Prompt.Text, null, null),
        Answered answered => new TurnEntry("answered", answered.Text, Name(answered.Ending), Detail(answered.Ending)),
        _ => throw new ArgumentOutOfRangeException(nameof(turn), turn, "Only asked and answered turns are archived."),
    };

    private static string Name(Ending ending) => ending switch
    {
        Finished => "finished",
        Stopped => "stopped",
        CutShort => "cutShort",
        Refused => "refused",
        _ => "failed",
    };

    private static string? Detail(Ending ending) => ending switch
    {
        Refused refused => refused.Category,
        Failed failed => failed.Reason,
        _ => null,
    };
}

internal sealed record ChatEntry(Guid Id, string Agent, string Title, string? SessionId, List<TurnEntry> Turns)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Adjustments { get; init; }
}

internal sealed record TurnEntry(string Kind, string Text, string? Ending, string? Detail);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<ChatEntry>))]
internal sealed partial class ChatJson : JsonSerializerContext;
