using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop.Tests.WindowManagers;

public sealed class WindowManagerTest
{
    private const string Hyprland = "HYPRLAND_INSTANCE_SIGNATURE=abc";

    private const string WaylandSession = "WAYLAND_DISPLAY=wayland-1";

    [Fact]
    public void TellsHyprlandFromAStackingWindowManager()
    {
        Assert.IsType<HyprlandWindowManager>(WindowManager.Detect(Variables(Hyprland)));
        Assert.IsType<StackingWindowManager>(WindowManager.Detect(Variables("XDG_CURRENT_DESKTOP=GNOME")));
    }

    [Theory]
    [InlineData(true, true, Hyprland, WaylandSession)]
    [InlineData(false, true, WaylandSession)]
    [InlineData(false, true, Hyprland)]
    [InlineData(false, false, Hyprland, WaylandSession)]
    [InlineData(true, true, WaylandSession, "CRAFTER_WAYLAND=1")]
    [InlineData(false, true, Hyprland, WaylandSession, "CRAFTER_WAYLAND=0")]
    [InlineData(false, true, "CRAFTER_WAYLAND=1")]
    public void GoesNativeOnWaylandWhereTheWindowManagerWantsItOrTheOverrideSaysSo(bool native, bool linux, params string[] set)
    {
        var variable = Variables(set);

        Assert.Equal(native, WindowManager.UsesNativeWayland(WindowManager.Detect(variable), linux, variable));
    }

    private static Func<string, string?> Variables(params string[] set)
    {
        var values = set.Select(each => each.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);

        return name => values.GetValueOrDefault(name);
    }
}
