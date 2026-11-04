using System.Text.Json;
using System.Text.Json.Nodes;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Abilities;

// What Home's agent can do to Crafter: add, create, open and remove projects, rename them, change
// their icons, and read and write the skills library. Answers are JSON for the agent to read.
// A relative folder is read from the projects root set in Preferences.
public sealed class CrafterAbilities(WorkbenchViewModel workbench, LibraryViewModel library, IBranches branches)
{
    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true };

    public Told ListProjects() => Json(new JsonArray([.. workbench.Projects.Select(project => (JsonNode)new JsonObject
    {
        ["name"] = project.Name.Text,
        ["folder"] = project.Folder,
        ["branch"] = branches.Of(project),
        ["openChats"] = workbench.OpenChats(project),
    })]));

    public Told AddProject(string? folder, string? name)
    {
        if (Under(folder) is not { } full)
        {
            return Told.Failed("A project needs a folder.");
        }

        if (!Directory.Exists(full))
        {
            return Told.Failed($"There is no folder at {full}; create_project makes a new one.");
        }

        if (Refused(full) is { } refused)
        {
            return refused;
        }

        var added = workbench.Shelve(full, string.IsNullOrWhiteSpace(name) ? workbench.NameOf(full) : new ProjectName(name));

        return new Told($"Added {added.Name.Text} to Crafter, from {added.Folder}.");
    }

    // A new, empty folder; without one given it is the name, in lowercase and dashes, under the
    // projects root. Git and anything else in it are the chat's to do.
    public Told CreateProject(string? name, string? folder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Told.Failed("A project needs a name.");
        }

        var full = Under(folder) ?? Path.Combine(workbench.ProjectsRoot, Slug(name));
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            return Told.Failed($"{full} already holds files; add_project adds a folder that exists.");
        }

        if (Refused(full) is { } refused)
        {
            return refused;
        }

        try
        {
            Directory.CreateDirectory(full);
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            return Told.Failed($"Could not create {full}: {unwritable.Message}");
        }

        var created = workbench.Shelve(full, new ProjectName(name));

        return new Told($"Created {created.Folder} and added {created.Name.Text} to Crafter.");
    }

    public Told OpenProject(string? folder)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        workbench.Open(project);

        return new Told($"Opened {project.Name.Text}.");
    }

    // Takes it off the sidebar and ends its open chats; the folder stays as it is.
    public Told RemoveProject(string? folder)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        workbench.Remove(project);

        return new Told($"Removed {project.Name.Text} from Crafter; its folder stays at {project.Folder}.");
    }

    public Told RenameProject(string? folder, string? name)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Told.Failed("A project needs a name.");
        }

        var before = project.Name.Text;
        var renamed = workbench.Rename(project, new ProjectName(name));

        return new Told($"Renamed {before} to {renamed.Name.Text}.");
    }

    // A relative image is read from the folder of the asking agent, where it works.
    public Told SetProjectIcon(string caller, string? folder, string? image)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        if (string.IsNullOrWhiteSpace(image))
        {
            return Told.Failed("An icon needs an image file.");
        }

        var file = Path.GetFullPath(image, caller);
        if (!File.Exists(file))
        {
            return Told.Failed($"There is no file at {file}.");
        }

        if (!ImageEntryViewModel.Formats.Contains(Path.GetExtension(file)))
        {
            return Told.Failed($"Crafter shows {string.Join(", ", ImageEntryViewModel.Formats)} icons; {Path.GetFileName(file)} is none of them.");
        }

        try
        {
            var changed = workbench.ChangeIcon(project, file);

            return new Told($"{changed.Name.Text} now shows {Path.GetFileName(file)} as its icon, kept in {changed.Icon}.");
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            return Told.Failed($"Could not keep the icon in {project.Name.Text}: {unwritable.Message}");
        }
    }

    public Told LibraryStatus() => Json(new JsonObject
    {
        ["repository"] = library.Repository,
        ["status"] = library.Status,
        ["skills"] = library.Skills.Count,
        ["problems"] = new JsonArray([.. library.Problems.Select(problem => (JsonNode)$"{problem.Place}: {problem.Reason}")]),
    });

    public async Task<Told> SyncLibrary()
    {
        if (!library.IsLinked)
        {
            return Told.Failed(Unlinked);
        }

        await library.SyncCommand.ExecuteAsync(null);

        return library.HasFailed ? Told.Failed(library.Status) : new Told(library.Status);
    }

    public Told ListSkills() => Json(new JsonArray([.. library.Skills.Select(skill => (JsonNode)new JsonObject
    {
        ["id"] = skill.Id,
        ["name"] = skill.Name,
        ["description"] = skill.Description,
        ["whenToUse"] = skill.WhenToUse,
    })]));

    public Told ReadSkill(string? id)
    {
        if (library.Skills.FirstOrDefault(skill => skill.Id == id) is not { } skill)
        {
            return Told.Failed($"The library has no skill {id}; list_skills gives the ids.");
        }

        return Json(new JsonObject
        {
            ["id"] = skill.Id,
            ["name"] = skill.Name,
            ["description"] = skill.Description,
            ["whenToUse"] = skill.WhenToUse,
            ["instructions"] = File.ReadAllText(skill.Instructions),
        });
    }

    public async Task<Told> SaveSkill(SkillDraft skill)
    {
        return await library.Save(skill) switch
        {
            Synced synced => new Told($"Saved {skill.Id} to the library and pushed it ({synced.Commit})."),
            SyncFailed failed => Told.Failed($"Could not save {skill.Id}: {failed.Reason}"),
            _ => Told.Failed($"Could not save {skill.Id}."),
        };
    }

    private const string Unlinked = "No library repository is linked; link one in Preferences → Library.";

    private Project? Find(string? folder) =>
        Under(folder) is { } full ? workbench.Projects.FirstOrDefault(project => project.Folder == Path.TrimEndingDirectorySeparator(full)) : null;

    private string? Under(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Trim(), workbench.ProjectsRoot));

    private Told? Refused(string folder)
    {
        if (folder == workbench.Home.Folder)
        {
            return Told.Failed("That folder is Home's own.");
        }

        return workbench.Projects.FirstOrDefault(project => project.Folder == folder) is { } shelved
            ? Told.Failed($"{shelved.Name.Text} is already in Crafter, from {folder}.")
            : null;
    }

    // "Lorekeeper Desktop" goes in lorekeeper-desktop.
    private static string Slug(string name)
    {
        var words = new string([.. name.Trim().ToLowerInvariant().Select(letter => char.IsLetterOrDigit(letter) ? letter : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 0 ? string.Join('-', words) : "project";
    }

    private static Told Json(JsonNode answer) => new(answer.ToJsonString(Readable));
}

public sealed record Told(string Text, bool IsError = false)
{
    public static Told Failed(string reason) => new(reason, IsError: true);
}
