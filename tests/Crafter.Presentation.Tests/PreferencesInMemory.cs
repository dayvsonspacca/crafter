using Crafter.Core.Preferences;

namespace Crafter.Presentation.Tests;

internal sealed class PreferencesInMemory(UserPreferences stored) : IPreferencesStore
{
    public UserPreferences Load() => stored;

    public void Save(UserPreferences preferences) => stored = preferences;
}
