namespace Crafter.Presentation.Keyboard;

public sealed record Shortcut
{
    private Shortcut(string keys) => Keys = keys;

    public string Keys { get; }

    public static Shortcut Of(string keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keys);

        return new Shortcut(keys.Trim());
    }

    public override string ToString() => Keys;
}
