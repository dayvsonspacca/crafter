using Avalonia.Styling;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Themes;

public static class ThemeVariants
{
    public static readonly ThemeVariant Redstone = Dark(Theme.Redstone);

    public static readonly ThemeVariant Deepslate = Dark(Theme.Deepslate);

    public static readonly ThemeVariant Nether = Dark(Theme.Nether);

    public static readonly ThemeVariant Warped = Dark(Theme.Warped);

    public static readonly ThemeVariant Copper = Dark(Theme.Copper);

    public static readonly ThemeVariant RedstoneLight = Light(Theme.Redstone);

    public static readonly ThemeVariant DeepslateLight = Light(Theme.Deepslate);

    public static readonly ThemeVariant NetherLight = Light(Theme.Nether);

    public static readonly ThemeVariant WarpedLight = Light(Theme.Warped);

    public static readonly ThemeVariant CopperLight = Light(Theme.Copper);

    private static readonly IReadOnlyDictionary<string, (ThemeVariant Dark, ThemeVariant Light)> ById =
        new Dictionary<string, (ThemeVariant, ThemeVariant)>
        {
            [Theme.Redstone.Id] = (Redstone, RedstoneLight),
            [Theme.Deepslate.Id] = (Deepslate, DeepslateLight),
            [Theme.Nether.Id] = (Nether, NetherLight),
            [Theme.Warped.Id] = (Warped, WarpedLight),
            [Theme.Copper.Id] = (Copper, CopperLight),
        };

    public static ThemeVariant For(Theme theme, Appearance appearance)
    {
        var (dark, light) = ById.GetValueOrDefault(theme.Id, (Redstone, RedstoneLight));

        return appearance == Appearance.Light ? light : dark;
    }

    private static ThemeVariant Dark(Theme theme) => new(theme.Name, ThemeVariant.Dark);

    private static ThemeVariant Light(Theme theme) => new($"{theme.Name} Light", ThemeVariant.Light);
}
