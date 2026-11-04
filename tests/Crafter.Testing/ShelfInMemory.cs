using Crafter.Core.Projects;

namespace Crafter.Testing;

public sealed class ShelfInMemory(params Shelved[] stored) : IBookshelfStore
{
    public IReadOnlyList<Shelved> Saved { get; private set; } = stored;

    public int Saves { get; private set; }

    public IReadOnlyList<Shelved> Load() => Saved;

    public void Save(IReadOnlyList<Shelved> shelf)
    {
        Saved = [.. shelf];
        Saves++;
    }
}
