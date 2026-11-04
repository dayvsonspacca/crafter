namespace Crafter.Core.Preferences;

public interface IPreferencesStore
{
    UserPreferences Load();

    void Save(UserPreferences preferences);
}
