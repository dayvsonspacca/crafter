using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Crafter.Desktop.Controls;

// The composer's text box with a caret that never covers a letter. Avalonia's TextPresenter
// floors the caret's x and pulls it one more pixel left at the end of a line, so it ate
// the last column of the glyph before it; this one draws its own caret on the first whole
// pixel after the glyph, and blinks it like the original.
public sealed class PromptBox : TextBox
{
    private static readonly TimeSpan Blink = TimeSpan.FromMilliseconds(500);

    private readonly Rectangle caret = new()
    {
        Width = 1,
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private readonly DispatcherTimer blink;

    private TextPresenter? presenter;

    public PromptBox()
    {
        CaretBrush = Brushes.Transparent;
        caret.Bind(Shape.FillProperty, this.GetObservable(ForegroundProperty));
        blink = new DispatcherTimer(Blink, DispatcherPriority.Normal, (_, _) => caret.Opacity = caret.Opacity > 0 ? 0 : 1);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (presenter is not null)
        {
            presenter.LayoutUpdated -= Place;
        }

        (caret.Parent as Panel)?.Children.Remove(caret);
        presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");

        // The presenter sits in a panel inside the text box's scroller, so the caret scrolls with the text.
        if (presenter?.Parent is Panel panel)
        {
            panel.Children.Add(caret);
            presenter.LayoutUpdated += Place;
        }

        Restart();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CaretIndexProperty
            || change.Property == TextProperty
            || change.Property == SelectionStartProperty
            || change.Property == SelectionEndProperty)
        {
            Restart();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Restart();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Restart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        blink.Stop();
    }

    // Any move or edit shows the caret at once and starts the blink over, as typing expects.
    private void Restart()
    {
        caret.Opacity = 1;
        blink.Stop();
        Place(null, EventArgs.Empty);

        if (caret.IsVisible)
        {
            blink.Start();
        }
    }

    private void Place(object? sender, EventArgs e)
    {
        var shown = presenter is not null && IsFocused && SelectionStart == SelectionEnd;
        caret.IsVisible = shown;
        if (!shown || presenter is null || caret.Parent is not Visual panel || presenter.TranslatePoint(default, panel) is not { } origin)
        {
            return;
        }

        var bounds = presenter.TextLayout.HitTestTextPosition(Math.Clamp(CaretIndex, 0, Text?.Length ?? 0));
        var x = Math.Min(Math.Ceiling(origin.X + bounds.X), Math.Max(0, origin.X + presenter.Bounds.Width - caret.Width));
        var margin = new Thickness(x, Math.Floor(origin.Y + bounds.Y), 0, 0);
        var height = Math.Ceiling(bounds.Height);

        if (caret.Margin != margin)
        {
            caret.Margin = margin;
        }

        if (!caret.Height.Equals(height))
        {
            caret.Height = height;
        }
    }
}
