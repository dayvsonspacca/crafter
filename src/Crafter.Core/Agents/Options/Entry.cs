namespace Crafter.Core.Agents.Options;

public sealed record Entry(string Key, string Title, string About, string Fallback) : Setting(Key, Title, About)
{
    public string Value(Configuration configuration) => configuration[Key] ?? Fallback;
}
