using Crafter.Core.Projects;

namespace Crafter.Testing;

public sealed class SettingsInMemory : IProjectSettingsStore
{
    private readonly Dictionary<string, ProjectSettings> saved = [];

    private readonly Dictionary<string, string> summaries = [];

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

    public Dictionary<string, AgentInstructions> Instructions { get; } = [];

    public string? LoadSummary(string folder) => summaries.GetValueOrDefault(folder);

    public void SaveSummary(string folder, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            summaries.Remove(folder);
        }
        else
        {
            summaries[folder] = summary.Trim();
        }
    }

    public AgentInstructions? FindInstructions(string folder) => Instructions.GetValueOrDefault(folder);
}
