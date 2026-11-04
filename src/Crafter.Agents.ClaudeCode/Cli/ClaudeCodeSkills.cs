using System.Security.Cryptography;
using System.Text;
using Crafter.Core.Conversations;
using Crafter.Core.Library;

namespace Crafter.Agents.ClaudeCode.Cli;

// Crafter's skills, written the way Claude Code reads them: a plugin folder for each project, made
// again from the library every time a session starts and handed over with --plugin-dir. Nothing
// lands in the project. Claude Code knows each skill by its description and reads the rest when
// it needs it, as /crafter:<id>.
public static class ClaudeCodeSkills
{
    public const string Plugin = "crafter";

    private const int Longest = 1024;

    private static readonly HashSet<string> Described = new(StringComparer.Ordinal) { "skill.json", "instructions.md" };

    // The folder that holds the plugin of the project in that folder.
    public static string FolderFor(string plugins, string project) =>
        Path.Combine(plugins, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(project)))[..16]);

    // Writes the plugin and says where it is; with no skills there is no plugin.
    public static string? Write(string folder, IReadOnlyList<Skill> skills)
    {
        if (skills.Count == 0)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            return null;
        }

        var manifest = Path.Combine(folder, ".claude-plugin", "plugin.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, $$"""
            {
              "name": "{{Plugin}}",
              "version": "1.0.0",
              "description": "The skills Crafter gives this project from its library."
            }

            """);

        var home = Path.Combine(folder, "skills");
        Directory.CreateDirectory(home);
        foreach (var gone in Directory.GetDirectories(home).Where(place => skills.All(skill => skill.Id != Path.GetFileName(place))))
        {
            Directory.Delete(gone, recursive: true);
        }

        foreach (var skill in skills)
        {
            Place(skill, Path.Combine(home, skill.Id));
        }

        return folder;
    }

    // A skill the user calls goes by its plugin's name, so it never reaches a Claude Code command
    // that happens to share the id, like /review.
    public static string Calling(Prompt prompt) => SkillCalls.Rewrite(prompt.Text, prompt.Skills, id => $"/{Plugin}:{id}");

    // What Claude Code reads: the description decides when, the instructions say how.
    public static string Markdown(Skill skill, string instructions) =>
        $"""
        ---
        name: {skill.Id}
        description: {Quoted(Description(skill))}
        ---

        {instructions.Trim()}

        """;

    private static void Place(Skill skill, string place)
    {
        if (Directory.Exists(place))
        {
            Directory.Delete(place, recursive: true);
        }

        Directory.CreateDirectory(place);
        var instructions = File.Exists(skill.Instructions) ? File.ReadAllText(skill.Instructions) : string.Empty;
        File.WriteAllText(Path.Combine(place, "SKILL.md"), Markdown(skill, instructions));

        // Scripts, templates and references the instructions point to come along as they are.
        foreach (var file in Directory.EnumerateFiles(skill.Folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(skill.Folder, file);
            if (Described.Contains(relative) || relative.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')))
            {
                continue;
            }

            var copy = Path.Combine(place, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy, overwrite: true);
        }
    }

    private static string Description(Skill skill)
    {
        var about = skill.WhenToUse is { } when ? $"{skill.Description.TrimEnd()} Use it when: {when}" : skill.Description;
        var flat = string.Join(' ', about.Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();

        return flat.Length > Longest ? flat[..(Longest - 1)] + "…" : flat;
    }

    private static string Quoted(string text) => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
