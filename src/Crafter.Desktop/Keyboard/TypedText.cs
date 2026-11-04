using Avalonia.Input;
using Avalonia.Interactivity;

namespace Crafter.Desktop.Keyboard;

// Avalonia's Wayland backend sends Escape (and other control keys nobody handled) on as typed
// text, and text boxes insert it as a box glyph. This drops control characters from typed text
// before any field sees it; tabs and line breaks stay.
public static class TypedText
{
    public static void Watch(InputElement root) =>
        root.AddHandler(InputElement.TextInputEvent, Filter, RoutingStrategies.Tunnel);

    internal static string Printable(string text) =>
        string.Concat(text.Where(letter => !char.IsControl(letter) || letter is '\t' or '\r' or '\n'));

    private static void Filter(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: > 0 } text)
        {
            return;
        }

        var printable = Printable(text);
        if (printable.Length == 0)
        {
            e.Handled = true;
        }
        else if (printable.Length != text.Length)
        {
            e.Text = printable;
        }
    }
}
