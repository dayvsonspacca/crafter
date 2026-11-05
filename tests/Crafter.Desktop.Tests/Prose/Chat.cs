using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Desktop.Prose;

namespace Crafter.Desktop.Tests.Prose;

// A chat column on a headless window, driven by mouse and keyboard at real text positions.
internal sealed class Chat
{
    private Chat(IEnumerable<Control> messages)
    {
        var column = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        column.Children.AddRange(messages);
        Scroller = new ScrollViewer { Content = column };
        Selection = TextSelection.Watch(Scroller);
        Window = new Window { Width = 800, Height = 600, Content = Scroller };
        Window.Show();
        Settle();
    }

    public Window Window { get; }

    public ScrollViewer Scroller { get; }

    public TextSelection Selection { get; }

    public static Chat Of(params string[] answers) => new(answers.Select(answer => new ProseView { Markdown = answer }));

    public static Chat With(params Control[] messages) => new(messages);

    public ProseText Block(string containing) =>
        Window.GetVisualDescendants().OfType<ProseText>().First(block => block.Plain.Contains(containing, StringComparison.Ordinal));

    public ProseText Marker(string item)
    {
        var text = Block(item);

        return Window.GetVisualDescendants().OfType<ProseText>().First(block => block.IsMarker && block.Row == text.Row);
    }

    // The point on the leading edge of a piece of a block's text, or right after it.
    public Point Before(string piece, string? within = null) => Edge(within ?? piece, piece, after: false);

    public Point After(string piece, string? within = null) => Edge(within ?? piece, piece, after: true);

    public Point LeftOf(ProseText block) => block.TranslatePoint(new Point(-3, block.Bounds.Height / 2), Window)!.Value;

    public void Drag(Point from, Point to)
    {
        Window.MouseDown(from, MouseButton.Left);
        Window.MouseMove(Mid(from, to), RawInputModifiers.LeftMouseButton);
        Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Window.MouseUp(to, MouseButton.Left);
        Settle();
    }

    public void Click(Point at, int times = 1, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        for (var click = 0; click < times; click++)
        {
            Window.MouseDown(at, MouseButton.Left, modifiers);
            Window.MouseUp(at, MouseButton.Left, modifiers);
        }

        Settle();
    }

    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.Control)
    {
        Window.KeyPressQwerty(key, modifiers);
        Settle();
    }

    public string? Copied()
    {
        Press(PhysicalKey.C);

        return Window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult();
    }

    public void Paste(string text)
    {
        Window.Clipboard!.SetTextAsync(text).GetAwaiter().GetResult();
        Settle();
    }

    private Point Edge(string within, string piece, bool after)
    {
        var block = Block(within);
        var index = block.Plain.IndexOf(piece, StringComparison.Ordinal) + (after ? piece.Length : 0);
        var caret = block.TextLayout.HitTestTextPosition(index);
        var local = new Point(after ? caret.X - 1 : caret.X + 1, caret.Y + (caret.Height / 2));

        return block.TranslatePoint(local, Window)!.Value;
    }

    private static Point Mid(Point one, Point other) => new((one.X + other.X) / 2, (one.Y + other.Y) / 2);

    public static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
