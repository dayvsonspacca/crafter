using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Preferences;

namespace Crafter.Storage;

public sealed class JsonPreferencesStore(string file, UserPreferences defaults) : IPreferencesStore
{
    public UserPreferences Load()
    {
        if (!File.Exists(file))
        {
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), PreferencesJson.Default.UserPreferences) ?? defaults;
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            JsonFile.SetAside(file);

            return defaults;
        }
    }

    public void Save(UserPreferences preferences)
    {
        JsonFile.Write(file, JsonSerializer.Serialize(preferences, PreferencesJson.Default.UserPreferences));
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(AgentIdJsonConverter), typeof(ConfigurationJsonConverter)])]
[JsonSerializable(typeof(UserPreferences))]
internal sealed partial class PreferencesJson : JsonSerializerContext;

internal sealed class AgentIdJsonConverter : JsonConverter<AgentId>
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

internal sealed class ConfigurationJsonConverter : JsonConverter<Configuration>
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
