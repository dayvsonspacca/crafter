using Avalonia;
using Crafter.Desktop.Themes;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Tests.Themes;

public sealed class AvaloniaThemeSwitcherTest
{
    [Fact]
    public void PaintsTheAssumedAppearanceUntilItSettles()
    {
        TestApp.Run(() =>
        {
            var switcher = new AvaloniaThemeSwitcher();
            var changes = 0;
            switcher.AppearanceChanged += (_, _) => changes++;

            switcher.Assume(Appearance.Dark);
            switcher.Apply(Theme.Copper);

            Assert.Equal(Appearance.Dark, switcher.Appearance);
            Assert.Equal(ThemeVariants.Copper, Application.Current!.RequestedThemeVariant);

            // The headless platform reports light and never says otherwise, like a light desktop.
            switcher.Settle();

            Assert.Equal(Appearance.Light, switcher.Appearance);
            Assert.Equal(ThemeVariants.CopperLight, Application.Current.RequestedThemeVariant);
            Assert.Equal(1, changes);
        });
    }

    [Fact]
    public void SettlesQuietlyWhenTheAssumptionWasRight()
    {
        TestApp.Run(() =>
        {
            var switcher = new AvaloniaThemeSwitcher();
            var changes = 0;
            switcher.AppearanceChanged += (_, _) => changes++;

            switcher.Assume(Appearance.Light);
            switcher.Settle();
            switcher.Settle();

            Assert.Equal(Appearance.Light, switcher.Appearance);
            Assert.Equal(0, changes);
        });
    }
}
