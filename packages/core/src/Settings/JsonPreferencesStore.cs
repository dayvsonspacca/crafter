using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Agents;

namespace Crafter.Core.Settings;

public sealed class JsonPreferencesStore(string file, Preferences defaults) : IPreferencesStore
{
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
    Converters = [typeof(AgentIdJson), typeof(ConfigurationJson)])]
[JsonSerializable(typeof(Preferences))]
internal sealed partial class PreferencesJson : JsonSerializerContext;

internal sealed class AgentIdJson : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    public override AgentId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value);
}

internal sealed class ConfigurationJson : JsonConverter<Configuration>
{
    public override Configuration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A configuration is an object of strings.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var key = reader.GetString()!;
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"The value of {key} is not a string.");
            }

            values[key] = reader.GetString()!;
        }

        return new Configuration(values);
    }

    public override void Write(Utf8JsonWriter writer, Configuration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var each in value.Values)
        {
            writer.WriteString(each.Key, each.Value);
        }

        writer.WriteEndObject();
    }
}
