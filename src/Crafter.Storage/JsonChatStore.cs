using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;

namespace Crafter.Storage;

public sealed class JsonChatStore(string folder) : IChatStore
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
            JsonFile.SetAside(file);

            return [];
        }
    }

    public void Save(Project project, IReadOnlyList<ChatRecord> chats)
    {
        List<ChatEntry> entries = [.. chats.Select(Entry)];
        JsonFile.Write(FileOf(project), JsonSerializer.Serialize(entries, ChatJson.Default.ListChatEntry));
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
            [.. entry.Turns.Select(Turn)]);

    private static ChatEntry Entry(ChatRecord record) =>
        new(record.Id.Value, record.Agent.Value, record.Title.Text, record.SessionId, [.. record.Turns.Select(Entry)]);

    private static Turn Turn(TurnEntry entry) => entry.Kind switch
    {
        "asked" => new Asked(new Prompt(entry.Text)),
        _ => new Answered(entry.Text, Ending(entry)) { Steps = [.. (entry.Steps ?? []).Select(Step).OfType<Reply>()] },
    };

    // A step that cannot be read back is left out; the answer's text still stands.
    private static Reply? Step(StepEntry step) => step switch
    {
        { Kind: "wrote", Text: { } text } => new Wrote(text),
        { Kind: "acting", Id: { } id, Tool: { } tool } => new Acting(id, tool, step.Summary ?? string.Empty, Changes(step)),
        { Kind: "acted", Id: { } id } => new Acted(id, step.Failed ?? false, step.Text ?? string.Empty),
        _ => null,
    };

    private static StepEntry? Entry(Reply step) => step switch
    {
        Wrote wrote => new StepEntry("wrote", Text: wrote.Fragment),
        Acting acting => new StepEntry("acting", Id: acting.Id, Tool: acting.Tool, Summary: acting.Summary, Added: acting.Changes?.Added, Removed: acting.Changes?.Removed),
        Acted acted => new StepEntry("acted", Id: acted.Id, Text: acted.Output, Failed: acted.Failed ? true : null),
        _ => null,
    };

    // Chats saved before the counts were kept hold them as one line, "+12 −3".
    private static Changes? Changes(StepEntry step) => step switch
    {
        { Added: { } added } => new Changes(added, step.Removed),
        { Stat: { } stat } when stat.Split(' ') is [var plus, .. var rest] && plus.StartsWith('+') && int.TryParse(plus[1..], CultureInfo.InvariantCulture, out var added) =>
            new Changes(added, rest is [var minus] && minus.StartsWith('−') && int.TryParse(minus[1..], CultureInfo.InvariantCulture, out var removed) ? removed : null),
        _ => null,
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
        Answered answered => new TurnEntry("answered", answered.Text, Name(answered.Ending), Detail(answered.Ending))
        {
            Steps = answered.Steps.Count > 0 ? [.. answered.Steps.Select(Entry).OfType<StepEntry>()] : null,
        },
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

internal sealed record ChatEntry(Guid Id, string Agent, string Title, string? SessionId, List<TurnEntry> Turns);

internal sealed record TurnEntry(string Kind, string Text, string? Ending, string? Detail)
{
    public List<StepEntry>? Steps { get; init; }
}

internal sealed record StepEntry(
    string Kind,
    string? Text = null,
    string? Id = null,
    string? Tool = null,
    string? Summary = null,
    string? Stat = null,
    bool? Failed = null,
    int? Added = null,
    int? Removed = null);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<ChatEntry>))]
internal sealed partial class ChatJson : JsonSerializerContext;
