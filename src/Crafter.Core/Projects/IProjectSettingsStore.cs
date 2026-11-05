namespace Crafter.Core.Projects;

public interface IProjectSettingsStore
{
    ProjectSettings? Load(string folder);

    void Save(string folder, ProjectSettings settings);

    string AdoptIcon(string folder, string image);

    void DropIcon(string folder);

    // The summary is prose of any length, so it is a file of its own beside the settings.
    string? LoadSummary(string folder);

    // No summary takes the file away.
    void SaveSummary(string folder, string? summary);

    AgentInstructions? FindInstructions(string folder);

    // The todos travel with the project too, in a file of their own since they change all day.
    IReadOnlyList<Todo> LoadTodos(string folder);

    // No todos take the file away.
    void SaveTodos(string folder, IReadOnlyList<Todo> todos);
}
