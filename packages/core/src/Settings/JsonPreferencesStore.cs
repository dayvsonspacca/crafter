using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Agents;

namespace Crafter.Core.Settings;

public sealed class JsonPreferencesStore(string file, Preferences defaults) : IPreferencesStore
{
    public static JsonPreferencesStore InUserConfig() =>
        new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "crafter", "preferences.json"),
            Preferences.Default(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    public Preferences Load()
    {
        if (!File.Exists(file))
        {
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), PreferencesJson.Default.Preferences) ?? defaults;
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            return defaults;
        }
    }

    public void Save(Preferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, JsonSerializer.Serialize(preferences, PreferencesJson.Default.Preferences));
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(AgentIdJson)])]
[JsonSerializable(typeof(Preferences))]
internal sealed partial class PreferencesJson : JsonSerializerContext;

internal sealed class AgentIdJson : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
