using Crafter.Core.Projects;

namespace Crafter.Storage;

// Reads the branch from the repository's HEAD file, so no git process runs every few seconds.
public sealed class GitBranches : IBranches
{
    private const string Reference = "ref: ";

    private const string Heads = "refs/heads/";

    private const string Pointer = "gitdir:";

    private const int ShortSha = 7;

    public string? Of(Project project)
    {
        try
        {
            if (Head(project.Folder) is not { } head || File.ReadLines(head).FirstOrDefault()?.Trim() is not { Length: > 0 } line)
            {
                return null;
            }

            if (!line.StartsWith(Reference, StringComparison.Ordinal))
            {
                return line[..Math.Min(ShortSha, line.Length)];
            }

            var name = line[Reference.Length..].Trim();

            return name.StartsWith(Heads, StringComparison.Ordinal) ? name[Heads.Length..] : name;
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The nearest .git up from the folder: a directory in a plain clone, a file that points
    // at the real one in a worktree or a submodule.
    private static string? Head(string folder)
    {
        for (var at = new DirectoryInfo(folder); at is not null; at = at.Parent)
        {
            var git = Path.Combine(at.FullName, ".git");
            if (Directory.Exists(git))
            {
                return Path.Combine(git, "HEAD");
            }

            if (File.Exists(git) && File.ReadLines(git).FirstOrDefault() is { } line && line.StartsWith(Pointer, StringComparison.Ordinal))
            {
                return Path.Combine(Path.GetFullPath(line[Pointer.Length..].Trim(), at.FullName), "HEAD");
            }
        }

        return null;
    }
}
