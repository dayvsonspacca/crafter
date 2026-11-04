using Avalonia.Controls;
using Avalonia.Input;

namespace Crafter.Desktop.Prose;

// One block of chat text: a paragraph, a list marker, a table cell, a prompt, a code block.
// It draws its selection but leaves the pointer to TextSelection, which selects across
// every block of the chat as one text.
public class ProseText : SelectableTextBlock
{
    public ProseText()
    {
        Focusable = false;
        ContextFlyout = null;
    }

    // Blocks of one list or table copy as lines, other blocks as paragraphs.
    public object? Group { get; set; }

    // Blocks on one line of a list or table: a marker and its item, the cells of a row.
    public object? Row { get; set; }

    public bool IsMarker { get; set; }

    public IReadOnlyList<LinkSpan> Links { get; set; } = [];

    public string Plain => (Inlines is { Count: > 0 } inlines ? inlines.Text : Text) ?? string.Empty;

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
    }
}

public sealed record LinkSpan(int Start, int End, string Url);
