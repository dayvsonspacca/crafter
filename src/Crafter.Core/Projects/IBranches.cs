namespace Crafter.Core.Projects;

// The branch a project's folder is on, or null outside a repository.
public interface IBranches
{
    string? Of(Project project);
}
