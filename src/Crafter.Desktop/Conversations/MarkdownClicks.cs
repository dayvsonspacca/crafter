using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;

namespace Crafter.Desktop.Conversations;

// Markdown.Avalonia only selects by dragging. This adds what every text view does:
// a double click selects the word under the pointer, a triple click the whole paragraph.
// Ctrl+C keeps working because the viewer copies whatever its paragraphs hold selected.
public sealed class MarkdownClicks
{
    private const double Still = 4;

    private Picked? picked;

    private MarkdownClicks(Control host)
    {
        host.AddHandler(InputElement.PointerPressedEvent, Press, RoutingStrategies.Bubble, handledEventsToo: true);
        host.AddHandler(InputElement.PointerReleasedEvent, Release, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public static void Watch(Control host) => _ = new MarkdownClicks(host);

    private void Press(object? sender, PointerPressedEventArgs e)
    {
        picked = null;

        if (e.ClickCount < 2
            || sender is not Visual host
            || !e.GetCurrentPoint(host).Properties.IsLeftButtonPressed
            || (e.Source as Visual)?.FindAncestorOfType<CTextBlock>(includeSelf: true) is not { } paragraph
            || paragraph.FindAncestorOfType<MarkdownScrollViewer>() is null)
        {
            return;
        }

        var range = e.ClickCount == 2 ? WordAt(paragraph, e.GetPosition(paragraph)) : Whole;
        if (range is null)
        {
            return;
        }

        picked = new Picked(paragraph, range.Value, e.GetPosition(host));
        picked.Apply();
    }

    // The viewer reselects from press to release when the button comes up, which
    // collapses the word; put it back unless the pointer was dragged meanwhile.
    private void Release(object? sender, PointerReleasedEventArgs e)
    {
        if (picked is { } chosen && sender is Visual host)
        {
            var moved = e.GetPosition(host) - chosen.From;
            if (Math.Abs(moved.X) < Still && Math.Abs(moved.Y) < Still)
            {
                chosen.Apply();
            }
        }

        picked = null;
    }

    private static readonly (int Begin, int End) Whole = (0, -1);

    private static (int Begin, int End)? WordAt(CTextBlock paragraph, Point at)
    {
        if (paragraph.CalcuatePointerFrom(at.X, at.Y) is not PhysicalTextPointer pointer)
        {
            return null;
        }

        return Word(paragraph.Text, pointer.Index);
    }

    // The run of word characters, spaces, or the single other character at a caret index.
    internal static (int Begin, int End)? Word(string text, int caret)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var at = Math.Clamp(caret, 0, text.Length - 1);
        if (at > 0 && Kind(text[at]) != CharKind.Word && Kind(text[at - 1]) == CharKind.Word)
        {
            at--;
        }

        var kind = Kind(text[at]);
        if (kind == CharKind.Other)
        {
            return (at, at + 1);
        }

        var begin = at;
        while (begin > 0 && Kind(text[begin - 1]) == kind)
        {
            begin--;
        }

        var end = at + 1;
        while (end < text.Length && Kind(text[end]) == kind)
        {
            end++;
        }

        return (begin, end);
    }

    private static CharKind Kind(char c) =>
        char.IsLetterOrDigit(c) || c == '_' ? CharKind.Word
        : char.IsWhiteSpace(c) ? CharKind.Space
        : CharKind.Other;

    private enum CharKind
    {
        Word,
        Space,
        Other,
    }

    private sealed record Picked(CTextBlock Paragraph, (int Begin, int End) Range, Point From)
    {
        public void Apply()
        {
            if (Range == Whole)
            {
                Paragraph.Select(Paragraph.GetBegin(), Paragraph.GetEnd());

                return;
            }

            try
            {
                Paragraph.Select(Range.Begin, Range.End);
            }
            catch (Exception)
            {
                // The library throws when a caret index lands inside an inline it measures
                // differently from its text, such as an image; leave the selection as it was.
            }
        }
    }
}
