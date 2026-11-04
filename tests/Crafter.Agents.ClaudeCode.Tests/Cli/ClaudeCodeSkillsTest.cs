using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Core.Conversations;
using Crafter.Core.Library;

namespace Crafter.Agents.ClaudeCode.Tests.Cli;

public sealed class ClaudeCodeSkillsTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-shelf-").FullName;

    private string Shelf => Path.Combine(home, "shelf");

    [Fact]
    public void CopiesTheSkillsAsTheLibraryKeepsThemButNotHiddenFiles()
    {
        var review = Skill("pr-review", "Reviews a pull request.", null, "Run scripts/diff.sh.");
        Directory.CreateDirectory(Path.Combine(review.Folder, "scripts"));
        File.WriteAllText(Path.Combine(review.Folder, "scripts", "diff.sh"), "git diff");
        File.WriteAllText(Path.Combine(review.Folder, ".DS_Store"), "junk");

        var shelf = ClaudeCodeSkills.Write(Shelf, [review])!;

        var place = Path.Combine(Shelf, "pr-review");
        Assert.Equal(
            ["instructions.md", Path.Combine("scripts", "diff.sh"), "skill.json"],
            Directory.EnumerateFiles(place, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(place, file)).Order(StringComparer.Ordinal));
        Assert.Equal(Path.Combine(place, "instructions.md"), shelf.InstructionsOf("pr-review"));
    }

    [Fact]
    public void KeepsOnlyTheSkillsGivenNowAndNothingWhenNoneAre()
    {
        var commit = Skill("commit", "Commits.", null, "Commit.");
        var review = Skill("pr-review", "Reviews.", null, "Review.");
        ClaudeCodeSkills.Write(Shelf, [commit, review]);

        ClaudeCodeSkills.Write(Shelf, [review]);
        Assert.False(Directory.Exists(Path.Combine(Shelf, "commit")));
        Assert.True(Directory.Exists(Path.Combine(Shelf, "pr-review")));

        Assert.Null(ClaudeCodeSkills.Write(Shelf, []));
        Assert.False(Directory.Exists(Shelf));
    }

    [Fact]
    public void ListsTheSkillsForTheSystemPromptAsTheOnlyOnes()
    {
        var shelf = ClaudeCodeSkills.Write(Shelf, [Skill("commit", "Writes commits\nthe team's way.", "Committing changes", "Commit.")])!;

        var catalog = ClaudeCodeSkills.Catalog(shelf);

        Assert.Contains("they are the only skills here", catalog);
        Assert.Contains($"- commit (commit): Writes commits the team's way. Use it when: Committing changes Instructions: {shelf.InstructionsOf("commit")}", catalog);
    }

    [Fact]
    public void PutsTheInstructionsOfACalledSkillAheadOfTheMessage()
    {
        var commit = Skill("commit", "Commits.", null, "Use conventional commits.");
        var shelf = ClaudeCodeSkills.Write(Shelf, [commit]);

        var sent = ClaudeCodeSkills.Calling(new Prompt("/commit the staged files") { Skills = [commit] }, shelf);

        Assert.Equal(
            $"<crafter-skill id=\"commit\" name=\"commit\" folder=\"{Path.Combine(Shelf, "commit")}\">\nUse conventional commits.\n</crafter-skill>\n\n/commit the staged files",
            sent.Text);
        Assert.Equal([commit], sent.Skills);
    }

    [Fact]
    public void SendsAMessageThatCallsNoSkillAsItIs()
    {
        var prompt = new Prompt("/home/hero/notes.md says what?");

        Assert.Same(prompt, ClaudeCodeSkills.Calling(prompt, shelf: null));
    }

    [Fact]
    public void GivesEachProjectItsOwnFolder()
    {
        Assert.NotEqual(ClaudeCodeSkills.FolderFor("/shelves", "/home/hero/crafter"), ClaudeCodeSkills.FolderFor("/shelves", "/home/hero/lorekeeper"));
        Assert.Equal(ClaudeCodeSkills.FolderFor("/shelves", "/home/hero/crafter"), ClaudeCodeSkills.FolderFor("/shelves", "/home/hero/crafter"));
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
