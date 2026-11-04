using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Tests.Fakes;

namespace Crafter.Presentation.Tests.Conversations.Composing;

public sealed class MentionsViewModelTest
{
    private readonly MentionsViewModel mentions = new(new FilesInMemory("README.md", "src/", "src/Core/", "src/Core/Prompt.cs", "src/Desktop/PromptBox.cs", "tests/PromptTest.cs"));

    [Fact]
    public void OpensWhenAWordStartsWithAnAt()
    {
        mentions.Look("look at @", 9);

        Assert.True(mentions.IsOpen);
        Assert.Equal("README.md", mentions.Selected?.Path);
    }

    [Fact]
    public void OffersOnlyCraftersSkillsAfterASlash()
    {
        var skilled = new MentionsViewModel(
            new FilesInMemory("README.md", "src/commit.cs"),
            () => [new("commit", "Commit", "Writes commits.", null, "/library/skills/commit"), new("pr-review", "PR review", "Reviews a pull request.", null, "/library/skills/pr-review")]);

        skilled.Look("/", 1);
        Assert.Equal(["commit", "pr-review"], skilled.Choices.Select(choice => choice.Path));
        Assert.All(skilled.Choices, choice => Assert.True(choice.IsSkill));

        skilled.Look("/rev", 4);
        Assert.Equal("pr-review", Assert.Single(skilled.Choices).Path);

        var (draft, caret) = skilled.Accept("/rev")!.Value;
        Assert.Equal("/pr-review ", draft);
        Assert.Equal(11, caret);
        Assert.False(skilled.IsOpen);
    }

    [Fact]
    public void OffersNothingForAPathOrWithoutSkills()
    {
        var skilled = new MentionsViewModel(new FilesInMemory("README.md"), () => [new("commit", "Commit", "Writes commits.", null, "/library/skills/commit")]);

        skilled.Look("/home/hero", 10);
        Assert.False(skilled.IsOpen);

        mentions.Look("/", 1);
        Assert.False(mentions.IsOpen);
    }

    [Fact]
    public void StaysClosedForAnEmailAddress()
    {
        mentions.Look("mail hero@crafter.dev", 21);

        Assert.False(mentions.IsOpen);
    }

    [Fact]
    public void PutsNamesThatStartWithTheQueryFirst()
    {
        mentions.Look("@prom", 5);

        Assert.Equal(["src/Core/Prompt.cs", "tests/PromptTest.cs", "src/Desktop/PromptBox.cs"], mentions.Choices.Select(choice => choice.Path));
    }

    [Fact]
    public void FindsPathsByScatteredLetters()
    {
        mentions.Look("@scp", 4);

        Assert.Equal("src/Core/Prompt.cs", mentions.Selected?.Path);
    }

    [Fact]
    public void ClosesWhenNothingMatches()
    {
        mentions.Look("@zzz", 4);

        Assert.False(mentions.IsOpen);
    }

    [Fact]
    public void AcceptingAFileWritesItsPathAndCloses()
    {
        mentions.Look("read @prom please", 10);

        var (draft, caret) = mentions.Accept("read @prom please")!.Value;

        Assert.Equal("read @src/Core/Prompt.cs  please", draft);
        Assert.Equal(25, caret);
        Assert.False(mentions.IsOpen);
    }

    [Fact]
    public void AcceptingAFolderKeepsLooking()
    {
        mentions.Look("@co", 3);

        var (draft, _) = mentions.Accept("@co")!.Value;

        Assert.Equal("@src/Core/", draft);
        Assert.True(mentions.IsOpen);
    }

    [Fact]
    public void MovesAroundTheChoices()
    {
        mentions.Look("@prom", 5);
        mentions.Move(1);

        Assert.Equal("tests/PromptTest.cs", mentions.Selected?.Path);

        mentions.Move(-2);

        Assert.Equal("src/Desktop/PromptBox.cs", mentions.Selected?.Path);
    }

    [Fact]
    public void ShowsTheNameApartFromItsFolder()
    {
        mentions.Look("@prom", 5);
        var first = mentions.Choices[0];

        Assert.Equal("Prompt.cs", string.Concat(first.Name.Select(part => part.Text)));
        Assert.Equal("Prom", first.Name.Single(part => part.IsHit).Text);
        Assert.Equal("src/Core", first.Folder);
        Assert.True(first.IsFile);
    }
}
