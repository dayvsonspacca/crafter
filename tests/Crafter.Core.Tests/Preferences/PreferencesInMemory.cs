using Crafter.Core.Preferences;

namespace Crafter.Core.Tests.Preferences;

internal sealed class PreferencesInMemory(UserPreferences stored) : IPreferencesStore
{
    public UserPreferences Saved { get; private set; } = stored;

    public int Loads { get; private set; }

    public int Saves { get; private set; }

    public UserPreferences Load()
    {
        Loads++;

        return Saved;
    }

    public void Save(UserPreferences preferences)
    {
        Saved = preferences;
        Saves++;
    }
}
