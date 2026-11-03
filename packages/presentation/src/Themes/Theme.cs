namespace Crafter.Presentation.Themes;

public sealed class Theme
{
    public static readonly Theme Redstone = new("redstone", "Redstone", "#141316", "#201E23", "#34313A", "#ECE6DE", "#9B948E", "#D0343F", "#C99A5B");

    public static readonly Theme Deepslate = new("deepslate", "Deepslate", "#0F1318", "#19202A", "#2A3441", "#E3E9F0", "#8794A5", "#6CC4E8", "#57C48F");

    public static readonly Theme Nether = new("nether", "Nether", "#170F10", "#24171A", "#3A2427", "#F0E4DA", "#A8928A", "#E8743B", "#F2C14E");

    public static readonly Theme Warped = new("warped", "Warped", "#0C1514", "#15221F", "#22362F", "#DCEAE5", "#84A39A", "#2DBFA0", "#E0628A");

    public static readonly Theme Copper = new("copper", "Copper", "#121514", "#1C2220", "#2C3532", "#E9E3D7", "#949C95", "#D98557", "#62AE98");

    private Theme(
        string id,
        string name,
        string background,
        string surface,
        string line,
        string foreground,
        string muted,
        string accent,
        string accentSecondary)
    {
        Id = id;
        Name = name;
        Background = background;
        Surface = surface;
        Line = line;
        Foreground = foreground;
        Muted = muted;
        Accent = accent;
        AccentSecondary = accentSecondary;
    }

    public static IReadOnlyList<Theme> All { get; } = [Redstone, Deepslate, Nether, Warped, Copper];

    public string Id { get; }

    public string Name { get; }

    public string Background { get; }

    public string Surface { get; }

    public string Line { get; }

    public string Foreground { get; }

    public string Muted { get; }

    public string Accent { get; }

    public string AccentSecondary { get; }

    public static Theme Named(string? id) => All.FirstOrDefault(theme => theme.Id == id) ?? Redstone;
}
