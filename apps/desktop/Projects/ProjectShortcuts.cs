using Crafter.Desktop.Keyboard;

namespace Crafter.Desktop.Projects;

public static class ProjectShortcuts
{
    public const int Slots = 9;

    public static readonly Shortcut Add = Shortcut.Of("Ctrl+K Ctrl+O");

    public static readonly Shortcut Close = Shortcut.Of("Ctrl+K F");

    public static readonly Shortcut Edit = Shortcut.Of("F2");

    public static Shortcut? Slot(int place) => place is >= 0 and < Slots ? Shortcut.Of($"Ctrl+{place + 1}") : null;
}
