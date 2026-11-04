namespace Crafter.Presentation.Themes;

public sealed class Theme
{
    public static readonly Theme Redstone = new(
        "redstone",
        "Redstone",
        new("#141316", "#201E23", "#34313A", "#ECE6DE", "#9B948E", "#D0343F", "#C99A5B"),
        new("#F8F5F2", "#EFEAE5", "#D8D0C8", "#1E1B1F", "#665E58", "#B3202C", "#86601F"));

    public static readonly Theme Deepslate = new(
        "deepslate",
        "Deepslate",
        new("#0F1318", "#19202A", "#2A3441", "#E3E9F0", "#8794A5", "#6CC4E8", "#57C48F"),
        new("#F4F6F9", "#E8ECF1", "#CBD3DD", "#141A22", "#556172", "#0F6E9E", "#1B7A4D"));

    public static readonly Theme Nether = new(
        "nether",
        "Nether",
        new("#170F10", "#24171A", "#3A2427", "#F0E4DA", "#A8928A", "#E8743B", "#F2C14E"),
        new("#FAF5F1", "#F2E9E3", "#DDCAC1", "#24171A", "#6E5A54", "#A9471A", "#7E5F0C"));

    public static readonly Theme Warped = new(
        "warped",
        "Warped",
        new("#0C1514", "#15221F", "#22362F", "#DCEAE5", "#84A39A", "#2DBFA0", "#E0628A"),
        new("#F1F7F5", "#E4EEEB", "#C3D6D0", "#0F1E1B", "#4A655D", "#0B735F", "#A8355C"));

    public static readonly Theme Copper = new(
        "copper",
        "Copper",
        new("#121514", "#1C2220", "#2C3532", "#E9E3D7", "#949C95", "#D98557", "#62AE98"),
        new("#F6F4EF", "#ECE8E0", "#D2CDC2", "#1A1E1C", "#5A625D", "#9C4C24", "#2A705C"));

    private Theme(string id, string name, ThemePalette dark, ThemePalette light)
    {
        Id = id;
        Name = name;
        Dark = dark;
        Light = light;
    }

    public static IReadOnlyList<Theme> All { get; } = [Redstone, Deepslate, Nether, Warped, Copper];

    public string Id { get; }

    public string Name { get; }

    public ThemePalette Dark { get; }

    public ThemePalette Light { get; }

    public static Theme Named(string? id) => All.FirstOrDefault(theme => theme.Id == id) ?? Redstone;

    public ThemePalette In(Appearance appearance) => appearance == Appearance.Light ? Light : Dark;
}
