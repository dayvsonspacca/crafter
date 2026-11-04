using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace Crafter.Desktop.Controls;

// How the cells move while crafting: a wave across the grid, or the bench, whose ring fills one cell
// at a time and folds into the centre.
public enum CellsMotion
{
    Wave,
    Bench,
}

// A 3x3 grid of cells drawn by hand. While crafting, a timer redraws it; otherwise it is a still
// picture. Style animations did the same once, but every grid kept them, even when done, and taking
// a chat down paid for each against all the others still on screen.
public sealed class CraftingCells : Control
{
    public static readonly StyledProperty<bool> IsCraftingProperty =
        AvaloniaProperty.Register<CraftingCells, bool>(nameof(IsCrafting));

    public static readonly StyledProperty<CellsMotion> MotionProperty =
        AvaloniaProperty.Register<CraftingCells, CellsMotion>(nameof(Motion));

    public static readonly StyledProperty<IBrush?> OnProperty =
        AvaloniaProperty.Register<CraftingCells, IBrush?>(nameof(On));

    public static readonly StyledProperty<IBrush?> OffProperty =
        AvaloniaProperty.Register<CraftingCells, IBrush?>(nameof(Off));

    public static readonly StyledProperty<IBrush?> FlashProperty =
        AvaloniaProperty.Register<CraftingCells, IBrush?>(nameof(Flash));

    public const double Cell = 5;

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    private static readonly int[] Ring = [0, 1, 2, 5, 8, 7, 6, 3];

    private readonly Stopwatch clock = new();

    private DispatcherTimer? timer;

    private bool attached;

    static CraftingCells()
    {
        AffectsRender<CraftingCells>(IsCraftingProperty, MotionProperty, OnProperty, OffProperty, FlashProperty);
    }

    public bool IsCrafting
    {
        get => GetValue(IsCraftingProperty);
        set => SetValue(IsCraftingProperty, value);
    }

    public CellsMotion Motion
    {
        get => GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    public IBrush? On
    {
        get => GetValue(OnProperty);
        set => SetValue(OnProperty, value);
    }

    public IBrush? Off
    {
        get => GetValue(OffProperty);
        set => SetValue(OffProperty, value);
    }

    public IBrush? Flash
    {
        get => GetValue(FlashProperty);
        set => SetValue(FlashProperty, value);
    }

    public bool IsTicking => timer is not null;

    // How far a cell is from off (0) to on (1) at this moment, and whether it is flashing.
    public static (double Lit, double Flash) Shade(CellsMotion motion, bool crafting, int cell, double seconds)
    {
        if (!crafting)
        {
            return motion == CellsMotion.Wave && cell != 4 ? (1, 0) : (0, 0);
        }

        return motion == CellsMotion.Wave ? (Wave(cell, seconds), 0) : Bench(cell, seconds);
    }

    public override void Render(DrawingContext context)
    {
        var slot = new Size(Bounds.Width / 3, Bounds.Height / 3);
        var seconds = clock.Elapsed.TotalSeconds;
        var off = ColorOf(Off);
        var on = ColorOf(On);
        var flash = ColorOf(Flash) ?? on;

        for (var cell = 0; cell < 9; cell++)
        {
            var (lit, flashing) = Shade(Motion, IsCrafting, cell, seconds);
            var brush = off is { } dim && on is { } bright
                ? new ImmutableSolidColorBrush(Mix(Mix(dim, bright, lit), flash ?? bright, flashing))
                : lit >= 0.5 ? On : Off;
            var left = ((cell % 3) * slot.Width) + ((slot.Width - Cell) / 2);
            var top = ((cell / 3) * slot.Height) + ((slot.Height - Cell) / 2);
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(left, top, Cell, Cell), 1));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        Animate();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        attached = false;
        Animate();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCraftingProperty)
        {
            Animate();
        }
    }

    // The timer runs only while crafting and on screen.
    private void Animate()
    {
        if (IsCrafting && attached)
        {
            if (timer is null)
            {
                clock.Restart();
                timer = new DispatcherTimer(Frame, DispatcherPriority.Render, (_, _) => InvalidateVisual());
                timer.Start();
            }

            return;
        }

        timer?.Stop();
        timer = null;
        clock.Reset();
    }

    // Each cell pulses on, off and on again every 1.2 s, starting a little after the one before.
    private static double Wave(int cell, double seconds)
    {
        const double Period = 1.2;
        var since = seconds - (cell * 0.13);
        if (since < 0)
        {
            return 0;
        }

        var phase = since % Period / Period;

        return phase < 0.5 ? 1 - (phase * 2) : (phase - 0.5) * 2;
    }

    // Every 2.4 s the ring lights one cell at a time, then folds into the centre, which flashes
    // and holds as the result.
    private static (double Lit, double Flash) Bench(int cell, double seconds)
    {
        const double Period = 2.4;
        var cue = seconds % Period / Period * 100;

        if (cell == 4)
        {
            return cue switch
            {
                < 61 => (0, 0),
                < 65 => (Ramp(cue, 61, 65), Ramp(cue, 61, 65)),
                < 72 => (1, 1 - Ramp(cue, 65, 72)),
                < 88 => (1, 0),
                < 96 => (1 - Ramp(cue, 88, 96), 0),
                _ => (0, 0),
            };
        }

        var at = 6 + (Array.IndexOf(Ring, cell) * 6);

        return cue switch
        {
            _ when cue < at - 2 => (0, 0),
            _ when cue < at => (Ramp(cue, at - 2, at), 0),
            < 60 => (1, 0),
            < 68 => (1 - Ramp(cue, 60, 68), 0),
            _ => (0, 0),
        };
    }

    private static double Ramp(double cue, double from, double to) => (cue - from) / (to - from);

    private static Color? ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : null;

    private static Color Mix(Color from, Color to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);

        return Color.FromArgb(
            (byte)Math.Round(from.A + ((to.A - from.A) * t)),
            (byte)Math.Round(from.R + ((to.R - from.R) * t)),
            (byte)Math.Round(from.G + ((to.G - from.G) * t)),
            (byte)Math.Round(from.B + ((to.B - from.B) * t)));
    }
}
