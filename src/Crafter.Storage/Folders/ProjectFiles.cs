using System.ComponentModel;
using System.Diagnostics;

namespace Crafter.Storage.Folders;

public static class ProjectFiles
{
    public const int Most = 20_000;

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "node_modules", "vendor", "bin", "obj", "dist", "target" };

    private static readonly EnumerationOptions Visible = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    public static IReadOnlyList<string> Of(string folder, CancellationToken stop = default)
    {
        var files = (FolderListing.IsRepository(folder) ? Tracked(folder, stop) : null) ?? Walked(folder, stop);

        return [.. WithFolders(files).Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> WithFolders(IReadOnlyList<string> files)
    {
        var folders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            yield return file;

            for (var slash = file.IndexOf('/'); slash > 0; slash = file.IndexOf('/', slash + 1))
            {
                var parent = file[..(slash + 1)];
                if (folders.Add(parent))
                {
                    yield return parent;
                }
            }
        }
    }

    private static List<string>? Tracked(string folder, CancellationToken stop)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["ls-files", "--cached", "--others", "--exclude-standard", "-z"])
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var git = Process.Start(start);
            if (git is null)
            {
                return null;
            }

            using var cancel = stop.Register(() => Kill(git));
            _ = git.StandardError.ReadToEndAsync(CancellationToken.None);
            var listed = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            stop.ThrowIfCancellationRequested();

            return git.ExitCode == 0 ? [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(Most)] : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static List<string> Walked(string folder, CancellationToken stop)
    {
        List<string> files = [];
        var pending = new Queue<string>();
        pending.Enqueue(folder);

        while (files.Count < Most && pending.TryDequeue(out var next))
        {
            stop.ThrowIfCancellationRequested();
            try
            {
                foreach (var entry in new DirectoryInfo(next).EnumerateFileSystemInfos("*", Visible))
                {
                    if (entry.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo child)
                    {
                        if (!Skipped.Contains(child.Name))
                        {
                            pending.Enqueue(child.FullName);
                        }
                    }
                    else if (files.Count < Most)
                    {
                        files.Add(Path.GetRelativePath(folder, entry.FullName).Replace(Path.DirectorySeparatorChar, '/'));
                    }
                }
            }
            catch (Exception unreachable) when (unreachable is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
            {
            }
        }

        return files;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
