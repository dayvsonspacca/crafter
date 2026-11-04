using Crafter.Presentation.Keyboard;

namespace Crafter.Presentation.Preferences;

public sealed class ShortcutsGroupViewModel() : PreferenceGroupViewModel("Shortcuts")
{
    public IReadOnlyList<ShortcutGroup> Groups => ShortcutCatalog.All;
}
