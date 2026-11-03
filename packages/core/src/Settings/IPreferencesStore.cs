namespace Crafter.Core.Settings;

public interface IPreferencesStore
{
    Preferences Load();

    void Save(Preferences preferences);
}
