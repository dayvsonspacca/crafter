using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Crafter.Core.Projects;

namespace Crafter.Storage;

// Instructions are the files agents read on their own, in the order one is preferred over another.
public sealed class JsonProjectSettingsStore(IReadOnlyList<string>? instructions = null) : IProjectSettingsStore
{
    private const string Home = ".crafter";

    private const string Icon = "icon";

    private const string Summary = "summary.md";

    private const string Todos = "todos.json";

    private static readonly JsonTypeInfo<List<TodoEntry>> TodosShape = (JsonTypeInfo<List<TodoEntry>>)new JsonSerializerOptions(ProjectSettingsJson.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }.GetTypeInfo(typeof(List<TodoEntry>));

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
                {
                    Axioms = [.. (entry.Axioms ?? []).Where(axiom => !string.IsNullOrWhiteSpace(axiom)).Select(axiom => axiom.Trim()).Distinct()],
                }
                : null;
        }
        catch (Exception unreadable) when (unreadable is JsonException or ArgumentException)
        {
            JsonFile.SetAside(file);

            return null;
        }
        catch (Exception unreachable) when (unreachable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string folder, ProjectSettings settings)
    {
        JsonFile.Write(FileIn(folder), JsonSerializer.Serialize(new SettingsEntry(settings.Name.Text, settings.Icon, settings.Axioms.Count > 0 ? [.. settings.Axioms] : null), Shape));
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

    public string? LoadSummary(string folder) => Read(Path.Combine(folder, Home, Summary));

    public void SaveSummary(string folder, string? summary)
    {
        var file = Path.Combine(folder, Home, Summary);
        if (string.IsNullOrWhiteSpace(summary))
        {
            File.Delete(file);
        }
        else
        {
            JsonFile.Write(file, summary.Trim() + "\n");
        }
    }

    // A todo without words or with a number another holds is left out; an unreadable file is set aside.
    public IReadOnlyList<Todo> LoadTodos(string folder)
    {
        var file = Path.Combine(folder, Home, Todos);
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            return [.. (JsonSerializer.Deserialize(File.ReadAllText(file), TodosShape) ?? [])
                .Where(entry => entry.Number > 0 && !string.IsNullOrWhiteSpace(entry.Text))
                .DistinctBy(entry => entry.Number)
                .Select(entry => new Todo(entry.Number, entry.Text.Trim(), entry.Done))];
        }
        catch (JsonException)
        {
            JsonFile.SetAside(file);

            return [];
        }
        catch (Exception unreachable) when (unreachable is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void SaveTodos(string folder, IReadOnlyList<Todo> todos)
    {
        var file = Path.Combine(folder, Home, Todos);
        if (todos.Count == 0)
        {
            File.Delete(file);
        }
        else
        {
            JsonFile.Write(file, JsonSerializer.Serialize([.. todos.Select(todo => new TodoEntry(todo.Number, todo.Text, todo.IsDone))], TodosShape));
        }
    }

    public AgentInstructions? FindInstructions(string folder) =>
        (instructions ?? [AgentInstructions.Shared]).Select(name => Read(Path.Combine(folder, name)) is { } text ? new AgentInstructions(name, text) : null).FirstOrDefault(found => found is not null);

    // A file's text without its surrounding blank space; a missing, empty or unreadable one is none.
    private static string? Read(string file)
    {
        try
        {
            return File.Exists(file) && File.ReadAllText(file).Trim() is { Length: > 0 } text ? text : null;
        }
        catch (Exception unreachable) when (unreachable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string FileIn(string folder) => Path.Combine(folder, Home, "settings.json");
}

internal sealed record SettingsEntry(string Name, string? Icon, List<string>? Axioms = null);

internal sealed record TodoEntry(int Number, string Text, bool Done = false);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SettingsEntry))]
[JsonSerializable(typeof(List<TodoEntry>))]
internal sealed partial class ProjectSettingsJson : JsonSerializerContext;
