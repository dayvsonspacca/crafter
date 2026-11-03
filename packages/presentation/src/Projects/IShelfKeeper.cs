using Crafter.Core.Projects;

namespace Crafter.Presentation.Projects;

public interface IShelfKeeper
{
    void Open(Project project);

    Task Edit(Project project);

    void Close();

    void Remove(Project project);

    Task CopyPath(Project project);
}
