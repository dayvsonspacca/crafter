namespace Crafter.Core.Projects;

public interface IProjectSettingsStore
{
    ProjectSettings? Load(string folder);

    void Save(string folder, ProjectSettings settings);

    string AdoptIcon(string folder, string image);

    void DropIcon(string folder);
}
