using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;

namespace Crafter.Core.Settings;

public sealed class PreferenceBook
{
    private readonly IPreferencesStore store;

    public PreferenceBook(IPreferencesStore store)
    {
        this.store = store;
        Current = store.Load();
    }

    public Preferences Current { get; private set; }

    public void Hire(IEnumerable<AgentId> agents) => Change(Current with { Agents = [.. agents] });

    public void Configure(AgentId agent, Configuration configuration) => Change(Current.Configure(agent, configuration));

    public void MoveProjectsRoot(string folder) => Change(Current with { ProjectsRoot = folder });

    public void ChooseTheme(string theme) => Change(Current with { Theme = theme });

    private void Change(Preferences changed)
    {
        if (changed == Current)
        {
            return;
        }

        Current = changed;
        store.Save(changed);
    }
}
