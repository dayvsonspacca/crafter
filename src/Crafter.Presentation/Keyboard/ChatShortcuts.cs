using Crafter.Presentation.Keyboard;

namespace Crafter.Presentation.Keyboard;

public static class ChatShortcuts
{
    public const int Slots = 9;

    public static readonly Shortcut New = Shortcut.Of("Ctrl+T");

    public static readonly Shortcut Close = Shortcut.Of("Ctrl+W");

    public static readonly Shortcut Next = Shortcut.Of("Ctrl+Tab");

    public static readonly Shortcut Previous = Shortcut.Of("Ctrl+Shift+Tab");

    public static readonly Shortcut ToggleJobs = Shortcut.Of("Ctrl+J");

    // The project's open chats in tab order, as Ctrl+1 to 9 reach the projects.
    public static Shortcut? Slot(int place) => place is >= 0 and < Slots ? Shortcut.Of($"Alt+{place + 1}") : null;
}
