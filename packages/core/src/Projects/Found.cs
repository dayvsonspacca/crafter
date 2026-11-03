namespace Crafter.Core.Projects;

public sealed record Found(Project Project, IReadOnlyList<int> NameHits, IReadOnlyList<int> FolderHits);
