using Crafter.Core.Settings;

namespace Crafter.Desktop.Tests;

internal sealed class PreferencesInMemory(Preferences stored) : IPreferencesStore
{
    public Preferences Load() => stored;

    public void Save(Preferences preferences) => stored = preferences;
}
