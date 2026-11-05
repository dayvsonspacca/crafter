using Crafter.Core.Projects;

namespace Crafter.Presentation.Projects;

// What a project's details page reads and keeps: its summary, the instructions file it may start
// from, and its name, icon and summary saved together.
public interface IProjectEditor
{
    string? SummaryOf(Project project);

    AgentInstructions? InstructionsIn(Project project);

    // A null summary leaves the project's as it is; an empty one takes it away.
    Project Save(Project project, ProjectName name, IconChange icon, string? summary);
}
