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
