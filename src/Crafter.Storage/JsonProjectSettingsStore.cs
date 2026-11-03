using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Crafter.Core.Projects;

namespace Crafter.Storage;

public sealed class JsonProjectSettingsStore : IProjectSettingsStore
{
    private const string Home = ".crafter";

    private const string Icon = "icon";

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
                ? new ProjectSettings(new ProjectName(entry.Name), entry.Icon)
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
        File.WriteAllText(file, JsonSerializer.Serialize(new SettingsEntry(settings.Name.Text, settings.Icon), Shape));
    }

    public string AdoptIcon(string folder, string image)
    {
        var kept = Path.Combine(folder, Home, Icon + Path.GetExtension(image).ToLowerInvariant());
        var source = Path.GetFullPath(image);

        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        if (source != Path.GetFullPath(kept))
        {
            DropIcon(folder);
            File.Copy(source, kept);
        }

        return $"{Home}/{Path.GetFileName(kept)}";
    }

    public void DropIcon(string folder)
    {
        var home = Path.Combine(folder, Home);
        if (!Directory.Exists(home))
        {
            return;
        }

        foreach (var icon in Directory.EnumerateFiles(home, Icon + ".*"))
        {
            File.Delete(icon);
        }
    }

    private static string FileIn(string folder) => Path.Combine(folder, Home, "settings.json");
}

internal sealed record SettingsEntry(string Name, string? Icon);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SettingsEntry))]
internal sealed partial class ProjectSettingsJson : JsonSerializerContext;
