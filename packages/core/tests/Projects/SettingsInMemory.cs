using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

internal sealed class SettingsInMemory : IProjectSettingsStore
{
    private readonly Dictionary<string, ProjectSettings> saved = [];

    public ProjectSettings? Load(string folder) => saved.GetValueOrDefault(folder);

    public void Save(string folder, ProjectSettings settings) => saved[folder] = settings;
}
