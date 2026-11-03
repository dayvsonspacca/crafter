using Crafter.Core.Projects;

namespace Crafter.Desktop.Projects;

public interface IShelfKeeper
{
    void Open(Project project);

    Task Rename(Project project);

    void Close();

    void Remove(Project project);

    Task CopyPath(Project project);
}
