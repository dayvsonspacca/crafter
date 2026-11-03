namespace Crafter.Core.Agents;

public sealed record Choice : Setting
{
    public Choice(string key, string title, string about, IReadOnlyList<Option> options)
        : base(key, title, about)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("A choice needs at least one option.", nameof(options));
        }

        Options = options;
    }

    public IReadOnlyList<Option> Options { get; }

    public Option Chosen(Configuration configuration) =>
        Options.FirstOrDefault(option => option.Value == configuration[Key]) ?? Options[0];
}
