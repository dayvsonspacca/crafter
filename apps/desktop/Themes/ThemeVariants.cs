using Avalonia.Styling;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Themes;

public static class ThemeVariants
{
    public static readonly ThemeVariant Redstone = Of(Theme.Redstone);

    public static readonly ThemeVariant Deepslate = Of(Theme.Deepslate);

    public static readonly ThemeVariant Nether = Of(Theme.Nether);

    public static readonly ThemeVariant Warped = Of(Theme.Warped);

    public static readonly ThemeVariant Copper = Of(Theme.Copper);

    private static readonly IReadOnlyDictionary<string, ThemeVariant> ById = new Dictionary<string, ThemeVariant>
    {
        [Theme.Redstone.Id] = Redstone,
        [Theme.Deepslate.Id] = Deepslate,
        [Theme.Nether.Id] = Nether,
        [Theme.Warped.Id] = Warped,
        [Theme.Copper.Id] = Copper,
    };

    public static ThemeVariant For(Theme theme) => ById.GetValueOrDefault(theme.Id) ?? Redstone;

    private static ThemeVariant Of(Theme theme) => new(theme.Name, ThemeVariant.Dark);
}
