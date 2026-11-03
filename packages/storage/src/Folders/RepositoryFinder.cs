namespace Crafter.Storage.Folders;

public sealed class RepositoryFinder(int depth = RepositoryFinder.UsualDepth)
{
    public const int UsualDepth = 4;

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "node_modules", "vendor", "bin", "obj" };

    public IEnumerable<string> Find(string root, CancellationToken stop = default)
    {
        var pending = new Queue<(string Folder, int Level)>();
        pending.Enqueue((root, 0));

        while (pending.TryDequeue(out var next))
        {
            stop.ThrowIfCancellationRequested();

            foreach (var child in FolderListing.Children(next.Folder))
            {
                if (Skipped.Contains(Path.GetFileName(child)))
                {
                    continue;
                }

                if (FolderListing.IsRepository(child))
                {
                    yield return child;

                    continue;
                }

                if (next.Level + 1 < depth)
                {
                    pending.Enqueue((child, next.Level + 1));
                }
            }
        }
    }
}
