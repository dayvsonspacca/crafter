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

        return new Shortcut(keys, Gesture(gestures[0]), gestures.Length > 1 ? Gesture(gestures[1]) : null);
    }

    private static KeyGesture Gesture(string keys)
    {
        var plus = keys.LastIndexOf('+');
        var key = keys[(plus + 1)..];
        var named = key switch
        {
            "," => "OemComma",
            [var digit] when char.IsAsciiDigit(digit) => $"D{digit}",
            _ => key,
        };

        return KeyGesture.Parse(keys[..(plus + 1)] + named);
    }

    public override string ToString() => Keys;
}
