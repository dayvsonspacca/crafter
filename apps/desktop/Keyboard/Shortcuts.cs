using Avalonia.Input;
using Crafter.Presentation.Keyboard;

namespace Crafter.Desktop.Keyboard;

public sealed class Shortcuts
{
    private readonly List<(Gestures Shortcut, Action Act)> bindings = [];

    private Gestures[] waiting = [];

    public void On(Shortcut shortcut, Action act) => bindings.Add((Gestures.Of(shortcut), act));

    public bool Handle(KeyEventArgs pressed)
    {
        if (IsModifier(pressed.Key))
        {
            return false;
        }

        if (waiting.Length > 0)
        {
            var started = waiting;
            waiting = [];

            Act(bindings.Where(binding => started.Contains(binding.Shortcut) && binding.Shortcut.Then!.Matches(pressed)));

            return true;
        }

        Gestures[] starting = [.. bindings
            .Where(binding => binding.Shortcut.Then is not null && binding.Shortcut.First.Matches(pressed))
            .Select(binding => binding.Shortcut)];

        if (starting.Length > 0)
        {
            waiting = starting;

            return true;
        }

        return Act(bindings.Where(binding => binding.Shortcut.Then is null && binding.Shortcut.First.Matches(pressed)));
    }

    private static bool Act(IEnumerable<(Gestures Shortcut, Action Act)> matching)
    {
        foreach (var (_, act) in matching.Take(1))
        {
            act();

            return true;
        }

        return false;
    }

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private sealed record Gestures(KeyGesture First, KeyGesture? Then)
    {
        public static Gestures Of(Shortcut shortcut)
        {
            var keys = shortcut.Keys.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return new Gestures(Gesture(keys[0]), keys.Length > 1 ? Gesture(keys[1]) : null);
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
    }
}
