using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crafter.Core.Projects;

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

            return [.. entries.Select(entry => new Shelved(new Project(entry.Folder), entry.LastOpened))];
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<Shelved> shelf)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);

        List<ShelfEntry> entries = [.. shelf.Select(shelved => new ShelfEntry(shelved.Project.Folder, shelved.LastOpened))];
        File.WriteAllText(file, JsonSerializer.Serialize(entries, BookshelfJson.Default.ListShelfEntry));
    }
}

internal sealed record ShelfEntry(string Folder, DateTimeOffset LastOpened);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<ShelfEntry>))]
internal sealed partial class BookshelfJson : JsonSerializerContext;
