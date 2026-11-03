using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

internal sealed class SettingsInMemory : IProjectSettingsStore
{
    private readonly Dictionary<string, ProjectSettings> saved = [];

    public ProjectSettings? Load(string folder) => saved.GetValueOrDefault(folder);

    public List<string> Adopted { get; } = [];

    public List<string> Dropped { get; } = [];

    public void Save(string folder, ProjectSettings settings) => saved[folder] = settings;

    public string AdoptIcon(string folder, string image)
    {
        Adopted.Add(image);

        return ".crafter/icon" + Path.GetExtension(image);
    }

    public void DropIcon(string folder) => Dropped.Add(folder);
}
