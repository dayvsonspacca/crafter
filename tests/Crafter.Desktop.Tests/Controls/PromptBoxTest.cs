using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Desktop.Controls;

namespace Crafter.Desktop.Tests.Controls;

public sealed class PromptBoxTest
{
    [Theory]
    [InlineData("rn", 2)]
    [InlineData("m mmm", 1)]
    [InlineData("Olá, bão?", 9)]
    public void DrawsTheCaretPastTheLetterWithoutCoveringIt(string text, int at)
    {
        TestApp.Run(() =>
        {
            var (window, box) = Show(text, at);
            var caret = Caret(box);
            var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
            var end = presenter.TextLayout.HitTestTextPosition(at).X;

            Assert.True(caret.IsVisible);
            Assert.Equal(Math.Ceiling(end), caret.Bounds.X);

            var with = Pixels(window);
            caret.Opacity = 0;
            Settle();
            var without = Pixels(window);

            // Only the caret's own column may differ: the letters keep every pixel.
            var column = (int)caret.TranslatePoint(default, window)!.Value.X;
            var changed = Enumerable.Range(0, with.Width * with.Height)
                .Where(index => with.Data[index] != without.Data[index])
                .Select(index => index % with.Width)
                .Distinct()
                .ToList();
            Assert.Equal([column], changed);
        });
    }

    [Fact]
    public void HidesAvaloniasCaretAndItsOwnWhenUnfocusedOrSelecting()
    {
        TestApp.Run(() =>
        {
            var (_, box) = Show("select me", 3);

            Assert.Equal(Brushes.Transparent, box.CaretBrush);

            box.SelectionStart = 0;
            box.SelectionEnd = 6;
            Settle();
            Assert.False(Caret(box).IsVisible);

            box.SelectionEnd = 0;
            box.CaretIndex = 4;
            Settle();
            Assert.True(Caret(box).IsVisible);
        });
    }

    private static (Window Window, PromptBox Box) Show(string text, int at)
    {
        var box = new PromptBox
        {
            Text = text,
            FontSize = 14,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            TextWrapping = TextWrapping.Wrap,
        };
        var window = new Window { Width = 240, Height = 60, Background = Brushes.Black, Content = new Border { Padding = new Thickness(16, 12), Child = box } };
        window.Show();
        box.Focus();
        box.CaretIndex = at;
        Settle();

        return (window, box);
    }

    private static Rectangle Caret(PromptBox box) => box.GetVisualDescendants().OfType<Rectangle>().Single();

    private static (int Width, int Height, uint[] Data) Pixels(Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var data = new uint[buffer.Size.Width * buffer.Size.Height];
        for (var row = 0; row < buffer.Size.Height; row++)
        {
            var line = new int[buffer.Size.Width];
            Marshal.Copy(buffer.Address + (row * buffer.RowBytes), line, 0, line.Length);
            for (var column = 0; column < line.Length; column++)
            {
                data[(row * buffer.Size.Width) + column] = (uint)line[column];
            }
        }

        return (buffer.Size.Width, buffer.Size.Height, data);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
