using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Crafter.Core.Projects;

public sealed class JsonProjectSettingsStore : IProjectSettingsStore
{
    private static readonly JsonTypeInfo<SettingsEntry> Shape = (JsonTypeInfo<SettingsEntry>)new JsonSerializerOptions(ProjectSettingsJson.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }.GetTypeInfo(typeof(SettingsEntry));

    public ProjectSettings? Load(string folder)
    {
        var file = FileIn(folder);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), Shape) is { } entry
                ? new ProjectSettings(new ProjectName(entry.Name))
                : null;
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string folder, ProjectSettings settings)
    {
        var file = FileIn(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(new SettingsEntry(settings.Name.Text), Shape));
    }

    private static string FileIn(string folder) => Path.Combine(folder, ".crafter", "settings.json");
}

internal sealed record SettingsEntry(string Name);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SettingsEntry))]
internal sealed partial class ProjectSettingsJson : JsonSerializerContext;
