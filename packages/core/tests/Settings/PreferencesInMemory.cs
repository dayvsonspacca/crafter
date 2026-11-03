using Crafter.Core.Settings;

namespace Crafter.Core.Tests.Settings;

internal sealed class PreferencesInMemory(Preferences stored) : IPreferencesStore
{
    public Preferences Saved { get; private set; } = stored;

    public int Loads { get; private set; }

    public int Saves { get; private set; }

    public Preferences Load()
    {
        Loads++;

        return Saved;
    }

    public void Save(Preferences preferences)
    {
        Saved = preferences;
        Saves++;
    }
}
