using System.Security.Cryptography;
using System.Text;
using Crafter.Core.Conversations;
using Crafter.Core.Library;

namespace Crafter.Agents.ClaudeCode.Cli;

// How Claude Code gets Crafter's skills, and only those: its own skills and slash commands are
// turned off, the project's skills are copied to a folder of Crafter's each time a session starts,
// the system prompt lists them, and a skill the user calls comes with the message itself.
public static class ClaudeCodeSkills
{
    public const string Instructions = "instructions.md";

    // The folder that holds the skills of the project in that folder.
    public static string FolderFor(string shelves, string project) =>
        Path.Combine(shelves, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(project)))[..16]);

    // Copies the skills, each a folder as the library keeps it; with none there is nothing to hand over.
    public static ClaudeCodeSkillShelf? Write(string folder, IReadOnlyList<Skill> skills)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        if (skills.Count == 0)
        {
            return null;
        }

        foreach (var skill in skills)
        {
            Copy(skill.Folder, Path.Combine(folder, skill.Id));
        }

        return new ClaudeCodeSkillShelf(folder, skills);
    }

    // What the system prompt says about the skills: what each is for and where to read it.
    public static string Catalog(ClaudeCodeSkillShelf shelf)
    {
        var text = new StringBuilder("""
            # Skills

            Crafter, the app this session runs in, gives this project the skills below, and they are the only skills here: Claude Code's own skills and slash commands are turned off. When a task fits a skill, read its instructions before starting and follow them. When a message carries a <crafter-skill> block, the user called that skill: follow it for that message.

            """);

        foreach (var skill in shelf.Skills)
        {
            text.Append($"\n- {skill.Id} ({skill.Name}): {Flat(skill.Description)}");
            if (skill.WhenToUse is { } when)
            {
                text.Append($" Use it when: {Flat(when)}");
            }

            text.Append($" Instructions: {shelf.InstructionsOf(skill.Id)}");
        }

        return text.Append('\n').ToString();
    }

    // A message that calls skills carries their instructions ahead of what the user wrote, so it
    // never opens with a slash and the agent has them at hand.
    public static Prompt Calling(Prompt prompt, ClaudeCodeSkillShelf? shelf)
    {
        if (prompt.Skills.Count == 0)
        {
            return prompt;
        }

        var text = new StringBuilder();
        foreach (var skill in prompt.Skills.Where(skill => File.Exists(skill.Instructions)))
        {
            var folder = shelf?.Skills.Any(each => each.Id == skill.Id) == true ? Path.Combine(shelf.Folder, skill.Id) : skill.Folder;
            text.Append($"<crafter-skill id=\"{skill.Id}\" name=\"{skill.Name}\" folder=\"{folder}\">\n")
                .Append(File.ReadAllText(skill.Instructions).Trim())
                .Append("\n</crafter-skill>\n\n");
        }

        return text.Length == 0 ? prompt : new Prompt(text.Append(prompt.Text).ToString(), prompt.Pictures) { Skills = prompt.Skills };
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        if (!Directory.Exists(from))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')))
            {
                continue;
            }

            var copy = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy, overwrite: true);
        }
    }

    private static string Flat(string text) => string.Join(' ', text.Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}

// The skills one session was given and the folder they were copied to.
public sealed record ClaudeCodeSkillShelf(string Folder, IReadOnlyList<Skill> Skills)
{
    public string InstructionsOf(string id) => Path.Combine(Folder, id, ClaudeCodeSkills.Instructions);
}
