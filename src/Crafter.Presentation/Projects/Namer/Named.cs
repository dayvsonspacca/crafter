using Crafter.Core.Projects;

namespace Crafter.Presentation.Projects.Namer;

// Summary is the one the user saved, when they changed it; null leaves the project's as it is.
public sealed record Named(ProjectName Name, IconChange Icon) : Naming
{
    public string? Summary { get; init; }
}
