using Avalonia.Media;
using Avalonia.Styling;

namespace Crafter.Desktop.Themes;

public sealed class Theme
{
    public static readonly ThemeVariant RedstoneVariant = new("Redstone", ThemeVariant.Dark);

    public static readonly ThemeVariant DeepslateVariant = new("Deepslate", ThemeVariant.Dark);

    public static readonly ThemeVariant NetherVariant = new("Nether", ThemeVariant.Dark);

    public static readonly ThemeVariant WarpedVariant = new("Warped", ThemeVariant.Dark);

    public static readonly ThemeVariant CopperVariant = new("Copper", ThemeVariant.Dark);

    public static readonly Theme Redstone = new("redstone", RedstoneVariant, "#141316", "#201E23", "#34313A", "#ECE6DE", "#9B948E", "#D0343F", "#C99A5B");

    public static readonly Theme Deepslate = new("deepslate", DeepslateVariant, "#0F1318", "#19202A", "#2A3441", "#E3E9F0", "#8794A5", "#6CC4E8", "#57C48F");

    public static readonly Theme Nether = new("nether", NetherVariant, "#170F10", "#24171A", "#3A2427", "#F0E4DA", "#A8928A", "#E8743B", "#F2C14E");

    public static readonly Theme Warped = new("warped", WarpedVariant, "#0C1514", "#15221F", "#22362F", "#DCEAE5", "#84A39A", "#2DBFA0", "#E0628A");

    public static readonly Theme Copper = new("copper", CopperVariant, "#121514", "#1C2220", "#2C3532", "#E9E3D7", "#949C95", "#D98557", "#62AE98");

    private Theme(
        string id,
        ThemeVariant variant,
        string background,
        string surface,
        string line,
        string foreground,
        string muted,
        string accent,
        string accentSecondary)
    {
        Id = id;
        Variant = variant;
        Background = Brush.Parse(background);
        Surface = Brush.Parse(surface);
        Line = Brush.Parse(line);
        Foreground = Brush.Parse(foreground);
        Muted = Brush.Parse(muted);
        Accent = Brush.Parse(accent);
        AccentSecondary = Brush.Parse(accentSecondary);
    }

    public static IReadOnlyList<Theme> All { get; } = [Redstone, Deepslate, Nether, Warped, Copper];

    public string Id { get; }

    public string Name => (string)Variant.Key;

    public ThemeVariant Variant { get; }

    public IBrush Background { get; }

    public IBrush Surface { get; }

    public IBrush Line { get; }

    public IBrush Foreground { get; }

    public IBrush Muted { get; }

    public IBrush Accent { get; }

    public IBrush AccentSecondary { get; }

    public static Theme Named(string? id) => All.FirstOrDefault(theme => theme.Id == id) ?? Redstone;
}
