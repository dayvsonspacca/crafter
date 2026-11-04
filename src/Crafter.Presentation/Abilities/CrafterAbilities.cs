using System.Text.Json;
using System.Text.Json.Nodes;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Abilities;

// What a project granted ManageCrafter can do to Crafter through its agent: look at the projects,
// rename them, and read and write the skills library. Answers are JSON for the agent to read.
public sealed class CrafterAbilities(WorkbenchViewModel workbench, LibraryViewModel library, IBranches branches)
{
    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true };

    public Told ListProjects(string caller) => Json(new JsonArray([.. workbench.Projects.Select(project => (JsonNode)new JsonObject
    {
        ["name"] = project.Name.Text,
        ["folder"] = project.Folder,
        ["branch"] = branches.Of(project),
        ["openChats"] = workbench.OpenChats(project),
        ["managesCrafter"] = workbench.Can(project, Capability.ManageCrafter),
        ["isYours"] = project.Folder == caller,
    })]));

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
        string.IsNullOrWhiteSpace(folder) ? null : workbench.Projects.FirstOrDefault(project => project == new Project(folder));

    private static Told Json(JsonNode answer) => new(answer.ToJsonString(Readable));
}

public sealed record Told(string Text, bool IsError = false)
{
    public static Told Failed(string reason) => new(reason, IsError: true);
}
