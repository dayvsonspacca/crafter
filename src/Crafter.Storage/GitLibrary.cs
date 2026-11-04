using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crafter.Core.Library;

namespace Crafter.Storage;

// The library repository mirrored in one folder. Nobody edits the mirror, so a sync takes the
// remote as it is: clone the first time or when the repository changes, then fetch and reset.
public sealed class GitLibrary(string folder) : ILibrary
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    public async Task<SyncOutcome> Sync(string repository, CancellationToken stop = default)
    {
        try
        {
            if (await IsCloneOf(repository, stop))
            {
                await Git(stop, "-C", folder, "fetch", "--quiet", "--prune", "origin");
                await Git(stop, "-C", folder, "reset", "--quiet", "--hard", "@{upstream}");
            }
            else
            {
                Clear();
                Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
                await Git(stop, "clone", "--quiet", repository, folder);
            }

            return new Synced((await Git(stop, "-C", folder, "rev-parse", "--short", "HEAD")).Trim());
        }
        catch (GitFailed failed)
        {
            return new SyncFailed(failed.Message);
        }
    }

    public LibraryContents Read()
    {
        var skills = Path.Combine(folder, "skills");
        if (!Directory.Exists(skills))
        {
            return LibraryContents.Empty;
        }

        List<Skill> found = [];
        List<LibraryProblem> problems = [];

        foreach (var place in Directory.GetDirectories(skills).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(place);
            var where = $"skills/{id}";

            if (Describe(place) is not { } about)
            {
                problems.Add(new LibraryProblem(where, "skill.json is missing or not valid JSON"));
            }
            else if (string.IsNullOrWhiteSpace(about.Description))
            {
                problems.Add(new LibraryProblem(where, "skill.json has no description"));
            }
            else if (!File.Exists(Path.Combine(place, "instructions.md")))
            {
                problems.Add(new LibraryProblem(where, "instructions.md is missing"));
            }
            else
            {
                var name = string.IsNullOrWhiteSpace(about.Name) ? id : about.Name.Trim();
                var when = string.IsNullOrWhiteSpace(about.WhenToUse) ? null : about.WhenToUse.Trim();
                found.Add(new Skill(id, name, about.Description.Trim(), when, place));
            }
        }

        return new LibraryContents([.. found.OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)], problems);
    }

    private static SkillJson? Describe(string place)
    {
        var file = Path.Combine(place, "skill.json");
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), LibraryJson.Default.SkillJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> IsCloneOf(string repository, CancellationToken stop)
    {
        if (!Directory.Exists(Path.Combine(folder, ".git")))
        {
            return false;
        }

        try
        {
            return (await Git(stop, "-C", folder, "remote", "get-url", "origin")).Trim() == repository;
        }
        catch (GitFailed)
        {
            return false;
        }
    }

    // The mirror goes before a fresh clone; git marks its objects read-only on some systems.
    private void Clear()
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(folder, recursive: true);
    }

    // Git never asks: a missing credential or an unknown host fails with git's own message.
    private static async Task<string> Git(CancellationToken stop, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (!start.Environment.ContainsKey("GIT_SSH_COMMAND"))
        {
            start.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";
        }

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            throw new GitFailed("git is not installed or not on the PATH");
        }

        if (process is null)
        {
            throw new GitFailed("git did not start");
        }

        using (process)
        using (var patience = CancellationTokenSource.CreateLinkedTokenSource(stop))
        {
            patience.CancelAfter(Patience);
            var output = process.StandardOutput.ReadToEndAsync(patience.Token);
            var error = process.StandardError.ReadToEndAsync(patience.Token);

            try
            {
                await process.WaitForExitAsync(patience.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new GitFailed(stop.IsCancellationRequested ? "the sync was stopped" : "git took too long and was stopped");
            }

            if (process.ExitCode != 0)
            {
                var said = (await error).Trim();
                throw new GitFailed(said.Length > 0 ? said.Split('\n')[^1].Trim() : $"git failed with code {process.ExitCode}");
            }

            return await output;
        }
    }

    private sealed class GitFailed(string message) : Exception(message);
}

internal sealed record SkillJson(string? Name, string? Description, string? WhenToUse);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SkillJson))]
internal sealed partial class LibraryJson : JsonSerializerContext;
