using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Projects;

namespace Crafter.Storage;

public sealed class JsonBookshelfStore(string file) : IBookshelfStore
{
    public IReadOnlyList<Shelved> Load()
    {
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize(File.ReadAllText(file), BookshelfJson.Default.ListShelfEntry) ?? [];

            return [.. entries.Select(entry => new Shelved(new Project(entry.Folder), entry.LastOpened)
            {
                Skills = [.. (entry.Skills ?? []).Where(skill => !string.IsNullOrWhiteSpace(skill)).Distinct()],
                Axioms = [.. (entry.Axioms ?? []).Where(axiom => !string.IsNullOrWhiteSpace(axiom)).Select(axiom => axiom.Trim()).Distinct()],
            })];
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            JsonFile.SetAside(file);

            return [];
        }
    }

    public void Save(IReadOnlyList<Shelved> shelf)
    {
        List<ShelfEntry> entries = [.. shelf.Select(shelved => new ShelfEntry(
            shelved.Project.Folder,
            shelved.LastOpened,
            shelved.Skills.Count > 0 ? [.. shelved.Skills] : null,
            shelved.Axioms.Count > 0 ? [.. shelved.Axioms] : null))];
        JsonFile.Write(file, JsonSerializer.Serialize(entries, BookshelfJson.Default.ListShelfEntry));
    }
}

// Projects once kept the capabilities they were granted; only Home manages Crafter now, so an
// old "capabilities" field is left unread and goes at the next save.
internal sealed record ShelfEntry(string Folder, DateTimeOffset LastOpened, List<string>? Skills = null, List<string>? Axioms = null);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<ShelfEntry>))]
internal sealed partial class BookshelfJson : JsonSerializerContext;
