namespace Crafter.Core.Agents;

public sealed class Configuration : IEquatable<Configuration>
{
    public static readonly Configuration Empty = new(new Dictionary<string, string>());

    private readonly Dictionary<string, string> values;

    public Configuration(IReadOnlyDictionary<string, string> values)
    {
        this.values = new Dictionary<string, string>(values.Where(value => value.Value.Trim().Length > 0), StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string> Values => values;

    public string? this[string key] => values.GetValueOrDefault(key);

    public Configuration With(string key, string? value)
    {
        var changed = new Dictionary<string, string>(values, StringComparer.Ordinal);
        if (value is null)
        {
            changed.Remove(key);
        }
        else
        {
            changed[key] = value;
        }

        return new Configuration(changed);
    }

    public bool Equals(Configuration? other) =>
        other is not null && values.Count == other.values.Count && values.All(value => other[value.Key] == value.Value);

    public override bool Equals(object? obj) => Equals(obj as Configuration);

    public override int GetHashCode() => values.Count;
}
