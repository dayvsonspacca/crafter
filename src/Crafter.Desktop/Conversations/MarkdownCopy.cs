using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ColorDocument.Avalonia;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;

namespace Crafter.Desktop.Conversations;

// Ctrl+C on an answer copies what was selected, and nothing else. Markdown.Avalonia
// gets two things wrong: it ends the text with a line break, and it starts the first
// list item with its marker (○, 1.) even when the selection began inside the item's
// text. The marker stays when the selection began on it or to its left, and items
// selected whole keep theirs, since their line was selected from its start.
public sealed partial class MarkdownCopy
{
    private const double SameLine = 4;

    private Edge? pressed;

    private Edge? released;

    private MarkdownCopy(Control host)
    {
        host.AddHandler(InputElement.PointerPressedEvent, Press, RoutingStrategies.Bubble, handledEventsToo: true);
        host.AddHandler(InputElement.PointerReleasedEvent, Release, RoutingStrategies.Bubble, handledEventsToo: true);
        host.AddHandler(InputElement.KeyDownEvent, Copy, RoutingStrategies.Tunnel);
    }

    public static void Watch(Control host) => _ = new MarkdownCopy(host);

    private void Press(object? sender, PointerPressedEventArgs e)
    {
        pressed = EdgeAt(sender, e);
        released = null;
    }

    private void Release(object? sender, PointerReleasedEventArgs e) => released = EdgeAt(sender, e);

    private async void Copy(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C
            || e.KeyModifiers is not (KeyModifiers.Control or KeyModifiers.Meta)
            || (e.Source as Visual)?.FindAncestorOfType<MarkdownScrollViewer>(includeSelf: true) is not { SelectionEnabled: true } viewer
            || Selected(viewer) is not { } text)
        {
            return;
        }

        e.Handled = true;

        if (StartsInItemText(viewer))
        {
            text = Marker().Replace(text, string.Empty, count: 1);
        }

        if (text.Length > 0 && TopLevel.GetTopLevel(viewer)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // A drag may run either way, so the selection starts at whichever end comes first.
    private bool StartsInItemText(MarkdownScrollViewer viewer)
    {
        if (pressed is not { } press || press.Viewer != viewer)
        {
            return false;
        }

        var start = released is { } release && release.Viewer == viewer && Before(release.At, press.At) ? release : press;

        return start.Spot == Spot.ItemText;
    }

    private static bool Before(Point one, Point other) =>
        Math.Abs(one.Y - other.Y) < SameLine ? one.X < other.X : one.Y < other.Y;

    private static Edge? EdgeAt(object? sender, PointerEventArgs e)
    {
        if (sender is not InputElement host
            || host.InputHitTest(e.GetPosition(host)) is not Visual hit
            || hit.FindAncestorOfType<MarkdownScrollViewer>(includeSelf: true) is not { } viewer)
        {
            return null;
        }

        return new Edge(viewer, e.GetPosition(viewer), SpotOf(hit));
    }

    private static Spot SpotOf(Visual hit)
    {
        if (hit.FindAncestorOfType<CTextBlock>(includeSelf: true) is { } block && block.Classes.Contains(ListMarker))
        {
            return Spot.Marker;
        }

        if (hit is Grid grid && grid.Classes.Contains(List))
        {
            return Spot.Marker;
        }

        return hit.GetVisualAncestors().OfType<Grid>().Any(grid => grid.Classes.Contains(List)) ? Spot.ItemText : Spot.Elsewhere;
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

    // ColorDocument's ClassNames for a list's grid and for the marker beside each item.
    private const string List = "List";

    private const string ListMarker = "ListMarker";

    // A marker as the viewer writes it at the start of the copied text: a symbol or a number with a dot, then a space.
    [GeneratedRegex(@"^(?:[•▪○❏]|\d+\.) ")]
    private static partial Regex Marker();

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_document")]
    private static extern ref DocumentElement? Document(MarkdownScrollViewer viewer);

    private enum Spot
    {
        Marker,
        ItemText,
        Elsewhere,
    }

    private sealed record Edge(MarkdownScrollViewer Viewer, Point At, Spot Spot);
}
