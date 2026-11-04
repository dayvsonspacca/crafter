using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;

namespace Crafter.Core.Preferences;

public sealed class PreferenceBook
{
    private readonly IPreferencesStore store;

    public PreferenceBook(IPreferencesStore store)
    {
        this.store = store;
        Current = store.Load();
    }

    public UserPreferences Current { get; private set; }

    public void Hire(IEnumerable<AgentId> agents) => Change(Current with { Agents = [.. agents] });

    public void Configure(AgentId agent, Configuration configuration) => Change(Current.Configure(agent, configuration));

    public void MoveProjectsRoot(string folder) => Change(Current with { ProjectsRoot = folder });

    public void ChooseTheme(string theme) => Change(Current with { Theme = theme });

    public void RememberSystem(bool dark) => Change(Current with { SystemDark = dark });

    public void LinkLibrary(string? repository) => Change(Current with { Library = repository });

    private void Change(UserPreferences changed)
    {
        if (changed == Current)
        {
            return;
        }

        Current = changed;
        store.Save(changed);
    }
}
