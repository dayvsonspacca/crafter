namespace Crafter.Core.Projects;

public sealed record Shelved(Project Project, DateTimeOffset LastOpened);
