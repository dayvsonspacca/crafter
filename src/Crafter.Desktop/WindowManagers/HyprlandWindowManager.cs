namespace Crafter.Desktop.WindowManagers;

public sealed class HyprlandWindowManager : WindowManager
{
    public override bool Minimizes => false;

    public override bool Maximizes => false;

    // Through XWayland, Hyprland hands drops from Wayland apps over wrong: the clipboard instead
    // of the dragged file. Talking to it directly, drops arrive as the file manager sent them.
    public override bool PrefersNativeWayland => true;
}
