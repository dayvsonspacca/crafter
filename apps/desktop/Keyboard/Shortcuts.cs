using Avalonia.Input;

namespace Crafter.Desktop.Keyboard;

public sealed class Shortcuts
{
    private readonly List<(Shortcut Shortcut, Action Act)> bindings = [];

    private Shortcut[] waiting = [];

    public void On(Shortcut shortcut, Action act) => bindings.Add((shortcut, act));

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

        Shortcut[] starting = [.. bindings
            .Where(binding => binding.Shortcut.Then is not null && binding.Shortcut.First.Matches(pressed))
            .Select(binding => binding.Shortcut)];

        if (starting.Length > 0)
        {
            waiting = starting;

            return true;
        }

        return Act(bindings.Where(binding => binding.Shortcut.Then is null && binding.Shortcut.First.Matches(pressed)));
    }

    private static bool Act(IEnumerable<(Shortcut Shortcut, Action Act)> matching)
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
}
