using Avalonia.Input;

namespace Crafter.Desktop.Keyboard;

public sealed class Shortcut
{
    private Shortcut(string keys, KeyGesture first, KeyGesture? then)
    {
        Keys = keys;
        First = first;
        Then = then;
    }

    public string Keys { get; }

    public KeyGesture First { get; }

    public KeyGesture? Then { get; }

    public static Shortcut Of(string keys)
    {
        var gestures = keys.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new Shortcut(keys, KeyGesture.Parse(gestures[0]), gestures.Length > 1 ? KeyGesture.Parse(gestures[1]) : null);
    }

    public override string ToString() => Keys;
}
