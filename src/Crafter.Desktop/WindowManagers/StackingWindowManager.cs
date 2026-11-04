namespace Crafter.Desktop.WindowManagers;

public sealed class StackingWindowManager : WindowManager
{
    public override bool Minimizes => true;

    public override bool Maximizes => true;

    public override bool PrefersNativeWayland => false;
}
