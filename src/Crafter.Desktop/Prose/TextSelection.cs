using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Crafter.Desktop.Prose;

// Selection over every ProseText in a scroller, the way a browser selects a page: press and
// drag across paragraphs, list items, table cells and messages; double click a word, triple
// click a block, shift click to extend; Ctrl+C, Ctrl+A, Escape and a context menu. The copy
// holds exactly what is selected: a list marker comes along only when the selection covers it.
public sealed class TextSelection
{
    private const double DragDistance = 3;

    private const double SameRow = 1;

    private static Cursor? hand;

    private readonly ScrollViewer scroller;

    private readonly DispatcherTimer edgeScroll;

    private readonly MenuItem copyItem;

    private readonly ContextMenu menu;

    private HashSet<ProseText> lit = [];

    private Caret? anchor;

    private Caret? focus;

    // The word or block a double or triple click picked; dragging on grows from it.
    private Picked? picked;

    private bool pressing;

    private bool dragged;

    private Point pressedAt;

    private Point pointerAt;

    private string? pressedLink;

    private TextSelection(ScrollViewer scroller)
    {
        this.scroller = scroller;
        scroller.Focusable = true;
        scroller.AddHandler(InputElement.PointerPressedEvent, Press, RoutingStrategies.Tunnel);
        scroller.AddHandler(InputElement.PointerMovedEvent, Move, RoutingStrategies.Tunnel, handledEventsToo: true);
        scroller.AddHandler(InputElement.PointerReleasedEvent, Release, RoutingStrategies.Tunnel, handledEventsToo: true);
        scroller.AddHandler(InputElement.KeyDownEvent, Command, RoutingStrategies.Bubble);
        scroller.AddHandler(Control.ContextRequestedEvent, Context, RoutingStrategies.Bubble);

        edgeScroll = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, Edge);

        copyItem = new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) };
        copyItem.Click += (_, _) => Copy();
        var all = new MenuItem { Header = "Select all", InputGesture = new KeyGesture(Key.A, KeyModifiers.Control) };
        all.Click += (_, _) => SelectAll();
        menu = new ContextMenu { ItemsSource = new[] { copyItem, all } };
    }

    public bool HasSelection => lit.Count > 0;

    // The selected text in reading order: blocks on one line of a list or table join with a
    // space or a tab, lines of one list or table with a line break, other blocks with a blank line.
    public string SelectedText
    {
        get
        {
            var builder = new StringBuilder();
            ProseText? previous = null;

            foreach (var block in Place().Select(placed => placed.Block).Where(lit.Contains))
            {
                var plain = block.Plain;
                var start = Math.Clamp(Math.Min(block.SelectionStart, block.SelectionEnd), 0, plain.Length);
                var end = Math.Clamp(Math.Max(block.SelectionStart, block.SelectionEnd), 0, plain.Length);
                if (end <= start)
                {
                    continue;
                }

                if (previous is not null)
                {
                    builder.Append(Between(previous, block));
                }

                builder.Append(plain, start, end - start);
                previous = block;
            }

            return builder.ToString();
        }
    }

    public static TextSelection Watch(ScrollViewer scroller) => new(scroller);

    public void Clear()
    {
        anchor = null;
        focus = null;
        picked = null;
        Light(null);
    }

    public void SelectAll()
    {
        var placed = Place();
        if (placed.Count == 0)
        {
            return;
        }

        picked = null;
        Take();
        anchor = new Caret(placed[0].Block, 0);
        focus = new Caret(placed[^1].Block, placed[^1].Block.Plain.Length);
        Light(placed);
    }

    public void Copy()
    {
        var text = SelectedText;
        if (text.Length > 0 && TopLevel.GetTopLevel(scroller)?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    // The run of word characters or of spaces a character belongs to, or that character alone.
    internal static (int Start, int End)? WordAround(string text, int character)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var at = Math.Clamp(character, 0, text.Length - 1);
        var kind = KindOf(text[at]);
        if (kind == CharKind.Other)
        {
            return (at, at + 1);
        }

        var start = at;
        while (start > 0 && KindOf(text[start - 1]) == kind)
        {
            start--;
        }

        var end = at + 1;
        while (end < text.Length && KindOf(text[end]) == kind)
        {
            end++;
        }

        return (start, end);
    }

    private static string Between(ProseText previous, ProseText next)
    {
        if (next.Row is not null && next.Row == previous.Row)
        {
            return previous.IsMarker ? " " : "\t";
        }

        return next.Group is not null && next.Group == previous.Group ? "\n" : "\n\n";
    }

    private void Press(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(scroller);
        if (!point.Properties.IsLeftButtonPressed || IsControl(e.Source) || scroller.Content is not Visual content)
        {
            return;
        }

        var placed = Place();
        var at = e.GetPosition(content);
        if (Locate(at, placed) is not { } caret)
        {
            return;
        }

        e.Handled = true;
        e.Pointer.Capture(scroller);
        pressing = true;
        dragged = false;
        pressedAt = pointerAt = point.Position;
        pressedLink = null;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && anchor is not null)
        {
            picked = null;
            focus = caret;
        }
        else if (e.ClickCount == 2 && WordAround(caret.Block.Plain, CharacterAt(placed, caret, at)) is var (start, end))
        {
            picked = new Picked(caret with { At = start }, caret with { At = end });
            (anchor, focus) = (picked.Value.Start, picked.Value.End);
        }
        else if (e.ClickCount >= 3)
        {
            picked = new Picked(caret with { At = 0 }, caret with { At = caret.Block.Plain.Length });
            (anchor, focus) = (picked.Value.Start, picked.Value.End);
        }
        else
        {
            picked = null;
            anchor = focus = caret;
            pressedLink = LinkUnder(placed, at);
        }

        // A plain click leaves the focus where it was, so typing in the composer goes on;
        // a selection takes it, so Ctrl+C reaches the chat.
        if (picked is not null || anchor != focus)
        {
            Take();
        }

        Light(placed);
    }

    private void Move(object? sender, PointerEventArgs e)
    {
        if (!pressing)
        {
            Hover(e);

            return;
        }

        pointerAt = e.GetPosition(scroller);
        var moved = pointerAt - pressedAt;
        if (!dragged && Math.Abs(moved.X) < DragDistance && Math.Abs(moved.Y) < DragDistance)
        {
            return;
        }

        dragged = true;
        pressedLink = null;
        Take();
        Extend();

        if (pointerAt.Y < 0 || pointerAt.Y > scroller.Viewport.Height)
        {
            edgeScroll.Start();
        }
        else
        {
            edgeScroll.Stop();
        }
    }

    private void Release(object? sender, PointerReleasedEventArgs e)
    {
        if (!pressing)
        {
            return;
        }

        pressing = false;
        edgeScroll.Stop();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (!dragged && pressedLink is { } url)
        {
            Open(url);
        }
    }

    private void Take() => scroller.Focus(NavigationMethod.Pointer);

    private void Extend()
    {
        if (scroller.Content is not Visual content || scroller.TranslatePoint(pointerAt, content) is not { } at)
        {
            return;
        }

        var placed = Place();
        if (Locate(at, placed) is not { } caret)
        {
            return;
        }

        if (picked is { } unit)
        {
            if (Compare(caret, unit.Start, placed) < 0)
            {
                (anchor, focus) = (unit.End, caret);
            }
            else
            {
                (anchor, focus) = (unit.Start, Compare(caret, unit.End, placed) > 0 ? caret : unit.End);
            }
        }
        else
        {
            focus = caret;
        }

        Light(placed);
    }

    // Dragging past the top or bottom scrolls the chat and keeps selecting.
    private void Edge(object? sender, EventArgs e)
    {
        var height = scroller.Viewport.Height;
        var beyond = pointerAt.Y < 0 ? pointerAt.Y : pointerAt.Y > height ? pointerAt.Y - height : 0;
        if (!pressing || beyond == 0)
        {
            edgeScroll.Stop();

            return;
        }

        var top = Math.Max(0, scroller.Extent.Height - height);
        scroller.Offset = new Vector(scroller.Offset.X, Math.Clamp(scroller.Offset.Y + (beyond / 2), 0, top));
        Extend();
    }

    private void Hover(PointerEventArgs e)
    {
        if (e.Source is not ProseText { Links.Count: > 0 } block)
        {
            return;
        }

        var hit = block.TextLayout.HitTestPoint(e.GetPosition(block));
        var index = hit.CharacterHit.FirstCharacterIndex;
        if (hit.IsInside && block.Links.Any(link => index >= link.Start && index < link.End))
        {
            block.Cursor = hand ??= new Cursor(StandardCursorType.Hand);
        }
        else
        {
            block.ClearValue(InputElement.CursorProperty);
        }
    }

    private void Command(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta;

        if (command && e.Key == Key.C && HasSelection)
        {
            e.Handled = true;
            Copy();
        }
        else if (command && e.Key == Key.A)
        {
            e.Handled = true;
            SelectAll();
        }
        else if (e.Key == Key.Escape && HasSelection)
        {
            e.Handled = true;
            Clear();
        }
    }

    private void Context(object? sender, ContextRequestedEventArgs e)
    {
        if (IsControl(e.Source))
        {
            return;
        }

        e.Handled = true;
        copyItem.IsEnabled = HasSelection;
        menu.Open(scroller);
    }

    private void Open(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
        {
            _ = TopLevel.GetTopLevel(scroller)?.Launcher.LaunchUriAsync(uri);
        }
    }

    // Presses on buttons, boxes and scroll bars stay theirs.
    private bool IsControl(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != scroller; visual = visual.GetVisualParent())
        {
            if (visual is Button or TextBox or ScrollBar or Thumb or ComboBox or Slider or MenuItem)
            {
                return true;
            }
        }

        return false;
    }

    private void Light(List<Placed>? placed)
    {
        HashSet<ProseText> now = [];

        if (placed is not null && anchor is { } one && focus is { } other)
        {
            var (from, to) = Compare(one, other, placed) <= 0 ? (one, other) : (other, one);
            var first = IndexOf(placed, from.Block);
            var last = IndexOf(placed, to.Block);

            for (var at = first; first >= 0 && last >= 0 && at <= last; at++)
            {
                var block = placed[at].Block;
                var start = at == first ? from.At : 0;
                var end = at == last ? to.At : block.Plain.Length;
                if (start == end)
                {
                    continue;
                }

                block.SelectionStart = start;
                block.SelectionEnd = end;
                now.Add(block);
            }
        }

        foreach (var block in lit.Where(block => !now.Contains(block)))
        {
            block.SelectionStart = 0;
            block.SelectionEnd = 0;
        }

        lit = now;
    }

    // Every visible block, in reading order: top to bottom, and left to right on one row.
    private List<Placed> Place()
    {
        List<Placed> placed = [];
        if (scroller.Content is not Visual content)
        {
            return placed;
        }

        foreach (var block in content.GetVisualDescendants().OfType<ProseText>())
        {
            if (block.IsEffectivelyVisible && block.Bounds.Width > 0 && block.TranslatePoint(default, content) is { } origin)
            {
                placed.Add(new Placed(block, new Rect(origin, block.Bounds.Size)));
            }
        }

        placed.Sort(static (one, other) => Math.Abs(one.Bounds.Top - other.Bounds.Top) < SameRow
            ? one.Bounds.Left.CompareTo(other.Bounds.Left)
            : one.Bounds.Top.CompareTo(other.Bounds.Top));

        return placed;
    }

    // The caret for a point: in the block on that row nearest to it, or between blocks the end
    // of the last block above, or the start of the first one.
    private static Caret? Locate(Point point, List<Placed> placed)
    {
        Placed? nearest = null;
        var distance = double.PositiveInfinity;

        foreach (var each in placed.Where(each => point.Y >= each.Bounds.Top && point.Y < each.Bounds.Bottom))
        {
            var away = point.X < each.Bounds.Left ? each.Bounds.Left - point.X
                : point.X > each.Bounds.Right ? point.X - each.Bounds.Right
                : 0;
            if (away < distance)
            {
                (nearest, distance) = (each, away);
            }
        }

        if (nearest is { } hit)
        {
            var position = hit.Block.TextLayout.HitTestPoint(point - hit.Bounds.TopLeft).TextPosition;

            return new Caret(hit.Block, Math.Clamp(position, 0, hit.Block.Plain.Length));
        }

        var above = placed.LastOrDefault(each => each.Bounds.Bottom <= point.Y);
        if (above.Block is not null)
        {
            return new Caret(above.Block, above.Block.Plain.Length);
        }

        return placed.Count > 0 ? new Caret(placed[0].Block, 0) : null;
    }

    // The character under a point; past the end of a line, the last one before the caret.
    private static int CharacterAt(List<Placed> placed, Caret caret, Point point)
    {
        var bounds = placed[IndexOf(placed, caret.Block)].Bounds;
        var hit = caret.Block.TextLayout.HitTestPoint(point - bounds.TopLeft);

        return hit.IsInside ? hit.CharacterHit.FirstCharacterIndex : caret.At - 1;
    }

    private static string? LinkUnder(List<Placed> placed, Point point)
    {
        foreach (var each in placed.Where(each => each.Block.Links.Count > 0 && each.Bounds.Contains(point)))
        {
            var hit = each.Block.TextLayout.HitTestPoint(point - each.Bounds.TopLeft);
            var index = hit.CharacterHit.FirstCharacterIndex;
            if (hit.IsInside)
            {
                return each.Block.Links.FirstOrDefault(link => index >= link.Start && index < link.End)?.Url;
            }
        }

        return null;
    }

    private static int Compare(Caret one, Caret other, List<Placed> placed)
    {
        var order = IndexOf(placed, one.Block).CompareTo(IndexOf(placed, other.Block));

        return order != 0 ? order : one.At.CompareTo(other.At);
    }

    private static int IndexOf(List<Placed> placed, ProseText block) => placed.FindIndex(each => each.Block == block);

    private static CharKind KindOf(char c) =>
        char.IsLetterOrDigit(c) || c == '_' ? CharKind.Word
        : char.IsWhiteSpace(c) ? CharKind.Space
        : CharKind.Other;

    private enum CharKind
    {
        Word,
        Space,
        Other,
    }

    private readonly record struct Caret(ProseText Block, int At);

    private readonly record struct Picked(Caret Start, Caret End);

    private readonly record struct Placed(ProseText Block, Rect Bounds);
}
