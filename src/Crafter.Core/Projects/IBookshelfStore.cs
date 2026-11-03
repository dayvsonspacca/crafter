namespace Crafter.Core.Projects;

public interface IBookshelfStore
{
    IReadOnlyList<Shelved> Load();

    void Save(IReadOnlyList<Shelved> shelf);
}
