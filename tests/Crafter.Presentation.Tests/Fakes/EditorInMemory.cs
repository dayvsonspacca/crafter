using Crafter.Core.Projects;
using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Tests.Fakes;

internal sealed class EditorInMemory : IProjectEditor
{
    public Dictionary<string, string> Summaries { get; } = [];

    public AgentInstructions? Instructions { get; set; }

    public List<(ProjectName Name, IconChange Icon, string? Summary)> Saved { get; } = [];

    public string? SummaryOf(Project project) => Summaries.GetValueOrDefault(project.Folder);

    public AgentInstructions? InstructionsIn(Project project) => Instructions;

    public Project Save(Project project, ProjectName name, IconChange icon, string? summary)
    {
        Saved.Add((name, icon, summary));
        if (summary is not null)
        {
            Summaries[project.Folder] = summary;
        }

        return new Project(project.Folder, name, icon is NoIcon ? null : project.Icon);
    }
}
