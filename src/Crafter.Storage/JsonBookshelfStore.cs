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
                Capabilities = [.. (entry.Capabilities ?? []).Select(Capabilities.Of).OfType<Capability>().Distinct()],
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
            shelved.Capabilities.Count > 0 ? [.. shelved.Capabilities.Select(Capabilities.Id)] : null))];
        JsonFile.Write(file, JsonSerializer.Serialize(entries, BookshelfJson.Default.ListShelfEntry));
    }
}

internal sealed record ShelfEntry(string Folder, DateTimeOffset LastOpened, List<string>? Capabilities = null);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<ShelfEntry>))]
internal sealed partial class BookshelfJson : JsonSerializerContext;
