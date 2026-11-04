using Crafter.Presentation.Keyboard;

namespace Crafter.Presentation.Tests.Keyboard;

public sealed class ShortcutCatalogTest
{
    [Fact]
    public void ReachesProjectsWithCtrlAndChatsWithAlt()
    {
        Assert.Equal("Ctrl+3", ProjectShortcuts.Slot(2)!.Keys);
        Assert.Equal("Alt+3", ChatShortcuts.Slot(2)!.Keys);
        Assert.Null(ChatShortcuts.Slot(9));
    }

    [Fact]
    public void ListsEveryBoundShortcut()
    {
        var keys = ShortcutCatalog.All.SelectMany(group => group.Entries).Select(entry => entry.Keys).ToList();

        Assert.Contains("Ctrl+1–9", keys);
        Assert.Contains("Alt+1–9", keys);
        Assert.All(
            [WorkbenchShortcuts.ToggleSidebar, WorkbenchShortcuts.TogglePreferences, ProjectShortcuts.Home, ProjectShortcuts.Add, ProjectShortcuts.Close, ProjectShortcuts.Edit,
             ChatShortcuts.New, ChatShortcuts.Close, ChatShortcuts.Next, ChatShortcuts.Previous, ChatShortcuts.ToggleJobs],
            shortcut => Assert.Contains(shortcut.Keys, keys));
    }

    [Theory]
    [InlineData("Ctrl+K Ctrl+O", new[] { "Ctrl", "K", "|", "Ctrl", "O" })]
    [InlineData("Ctrl+,", new[] { "Ctrl", "," })]
    [InlineData("Shift+Enter", new[] { "Shift", "Enter" })]
    [InlineData("+", new[] { "+" })]
    public void SplitsKeysIntoChordsOfCaps(string keys, string[] caps)
    {
        var chords = new ShortcutEntry("Anything", keys).Chords;

        Assert.Equal(caps, string.Join(" | ", chords.Select(chord => string.Join(" ", chord.Caps))).Split(' '));
    }
}
