namespace Crafter.Presentation.Keyboard;

// Every shortcut, worded for the Shortcuts page in Preferences. The bound ones come from the
// shortcut classes, so the page shows what the keys really do.
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutGroup> All { get; } =
    [
        new("Workbench",
        [
            new("Toggle the sidebar", WorkbenchShortcuts.ToggleSidebar.Keys),
            new("Open Preferences", WorkbenchShortcuts.TogglePreferences.Keys),
        ]),
        new("Projects",
        [
            new("Go Home", ProjectShortcuts.Home.Keys),
            new("Go to project 1 to 9", Range(ProjectShortcuts.Slot(0)!, ProjectShortcuts.Slots)),
            new("Add a project", ProjectShortcuts.Add.Keys),
            new("Rename the project", ProjectShortcuts.Edit.Keys),
            new("Close the project", ProjectShortcuts.Close.Keys),
        ]),
        new("Chats",
        [
            new("Go to chat 1 to 9", Range(ChatShortcuts.Slot(0)!, ChatShortcuts.Slots)),
            new("New chat", ChatShortcuts.New.Keys),
            new("Close the chat", ChatShortcuts.Close.Keys),
            new("Next chat", ChatShortcuts.Next.Keys),
            new("Previous chat", ChatShortcuts.Previous.Keys),
            new("Background jobs", ChatShortcuts.ToggleJobs.Keys),
        ]),
        new("Composer",
        [
            new("Craft", "Enter"),
            new("New line", "Shift+Enter"),
            new("Stop the agent and edit the prompt", "Escape"),
            new("Clear the draft", "Escape Escape"),
            new("Mention a file", "@"),
            new("Call a skill", "/"),
            new("Remove the last attachment, at the start of the draft", "Backspace"),
        ]),
        new("Chat text",
        [
            new("Jump to the latest message and follow the agent", "Ctrl+End"),
            new("Jump to the first message shown", "Ctrl+Home"),
            new("Copy the selection", "Ctrl+C"),
            new("Select all", "Ctrl+A"),
            new("Clear the selection", "Escape"),
        ]),
    ];

    // "Ctrl+1" for slots 1 to 9 reads "Ctrl+1–9".
    private static string Range(Shortcut first, int slots) => $"{first.Keys}–{slots}";
}

public sealed record KeyChord(IReadOnlyList<string> Caps);

public sealed record ShortcutGroup(string Name, IReadOnlyList<ShortcutEntry> Entries);

public sealed record ShortcutEntry(string Action, string Keys)
{
    // Each chord's keys, for key caps: "Ctrl+K Ctrl+O" is [Ctrl, K] then [Ctrl, O].
    public IReadOnlyList<KeyChord> Chords { get; } =
        [.. Keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(chord => new KeyChord(Caps(chord)))];

    // A lone "+" is the plus key, not a separator.
    private static string[] Caps(string chord) => chord == "+" ? ["+"] : chord.Split('+');
}
