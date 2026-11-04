using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ColorDocument.Avalonia;
using Markdown.Avalonia;

namespace Crafter.Desktop.Conversations;

// Markdown.Avalonia ends every copied block with a line break, the last one included,
// so a pasted selection always brought an extra empty line. Ctrl+C on an answer copies
// the same text the viewer would, without the trailing break.
public static class MarkdownCopy
{
    public static void Watch(Control host) =>
        host.AddHandler(InputElement.KeyDownEvent, Copy, RoutingStrategies.Tunnel);

    private static async void Copy(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C
            || e.KeyModifiers is not (KeyModifiers.Control or KeyModifiers.Meta)
            || (e.Source as Visual)?.FindAncestorOfType<MarkdownScrollViewer>(includeSelf: true) is not { SelectionEnabled: true } viewer
            || Selected(viewer) is not { } text)
        {
            return;
        }

        e.Handled = true;

        if (text.Length > 0 && TopLevel.GetTopLevel(viewer)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // Null hands the key back to the viewer, should a library update rename its document.
    private static string? Selected(MarkdownScrollViewer viewer)
    {
        try
        {
            return Document(viewer)?.GetSelectedText().TrimEnd();
        }
        catch (MissingFieldException)
        {
            return null;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_document")]
    private static extern ref DocumentElement? Document(MarkdownScrollViewer viewer);
}
