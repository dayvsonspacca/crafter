using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Core.Library;

namespace Crafter.Agents.ClaudeCode.Tests.Cli;

public sealed class ClaudeCodeSkillsTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-plugin-").FullName;

    private string Plugin => Path.Combine(home, "plugin");

    [Fact]
    public void WritesTheSkillsAsAClaudeCodePlugin()
    {
        var commit = Skill("commit", "Writes commits the way this team does.", "Committing changes", "Use conventional commits.");

        Assert.Equal(Plugin, ClaudeCodeSkills.Write(Plugin, [commit]));

        Assert.Contains("\"name\": \"crafter\"", File.ReadAllText(Path.Combine(Plugin, ".claude-plugin", "plugin.json")));
        Assert.Equal(
            "---\nname: commit\ndescription: \"Writes commits the way this team does. Use it when: Committing changes\"\n---\n\nUse conventional commits.\n",
            File.ReadAllText(Path.Combine(Plugin, "skills", "commit", "SKILL.md")).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void BringsTheSkillsOtherFilesButNotItsDescriptionOrHiddenOnes()
    {
        var review = Skill("pr-review", "Reviews a pull request.", null, "Run scripts/diff.sh.");
        Directory.CreateDirectory(Path.Combine(review.Folder, "scripts"));
        File.WriteAllText(Path.Combine(review.Folder, "scripts", "diff.sh"), "git diff");
        File.WriteAllText(Path.Combine(review.Folder, ".DS_Store"), "junk");

        ClaudeCodeSkills.Write(Plugin, [review]);

        var place = Path.Combine(Plugin, "skills", "pr-review");
        Assert.Equal(["SKILL.md", Path.Combine("scripts", "diff.sh")], Directory.EnumerateFiles(place, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(place, file)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void KeepsADescriptionOnOneQuotedLine()
    {
        var tricky = Skill("tricky", "Says \"hi\": then\nleaves.", null, "Body.");

        ClaudeCodeSkills.Write(Plugin, [tricky]);

        Assert.Contains("description: \"Says \\\"hi\\\": then leaves.\"\n", File.ReadAllText(Path.Combine(Plugin, "skills", "tricky", "SKILL.md")));
    }

    [Fact]
    public void DropsSkillsNoLongerGivenAndTheWholePluginWhenNoneAre()
    {
        var commit = Skill("commit", "Commits.", null, "Commit.");
        var review = Skill("pr-review", "Reviews.", null, "Review.");
        ClaudeCodeSkills.Write(Plugin, [commit, review]);

        ClaudeCodeSkills.Write(Plugin, [review]);
        Assert.False(Directory.Exists(Path.Combine(Plugin, "skills", "commit")));
        Assert.True(Directory.Exists(Path.Combine(Plugin, "skills", "pr-review")));

        Assert.Null(ClaudeCodeSkills.Write(Plugin, []));
        Assert.False(Directory.Exists(Plugin));
    }

    [Fact]
    public void GivesEachProjectItsOwnFolder()
    {
        Assert.NotEqual(ClaudeCodeSkills.FolderFor("/plugins", "/home/hero/crafter"), ClaudeCodeSkills.FolderFor("/plugins", "/home/hero/lorekeeper"));
        Assert.Equal(ClaudeCodeSkills.FolderFor("/plugins", "/home/hero/crafter"), ClaudeCodeSkills.FolderFor("/plugins", "/home/hero/crafter"));
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private Skill Skill(string id, string description, string? whenToUse, string instructions)
    {
        var folder = Directory.CreateDirectory(Path.Combine(home, "library", "skills", id)).FullName;
        File.WriteAllText(Path.Combine(folder, "skill.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "instructions.md"), instructions + "\n");

        return new Skill(id, id, description, whenToUse, folder);
    }
}
