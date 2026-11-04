using Crafter.Core.Projects;

namespace Crafter.Presentation.Projects.Namer;

public sealed record Named(ProjectName Name, IconChange Icon) : Naming;
