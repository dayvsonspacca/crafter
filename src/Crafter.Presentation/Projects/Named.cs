using Crafter.Core.Projects;

namespace Crafter.Presentation.Projects;

public sealed record Named(ProjectName Name, IconChange Icon) : Naming;
