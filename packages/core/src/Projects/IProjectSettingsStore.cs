namespace Crafter.Core.Projects;

public interface IProjectSettingsStore
{
    ProjectSettings? Load(string folder);

    void Save(string folder, ProjectSettings settings);
}
