using Crafter.Presentation.Conversations.Messages;

namespace Crafter.Presentation.Tests.Conversations.Messages;

public sealed class AnswerPiecesTest
{
    [Fact]
    public void SplitsProseFromCode()
    {
        Assert.Equal(
            [(null, "Run it:"), ("bash", "just test"), (null, "Done.")],
            AnswerPieces.Of("Run it:\n\n```bash\njust test\n```\n\nDone."));
    }

    [Fact]
    public void KeepsShorterFencesInsideALongerOne()
    {
        var answer = "The skill:\n\n````markdown\n# Commit\n\n```\nfeat(mcp): x\n```\n\nEnd of skill.\n````\n\nApprove it?";

        Assert.Equal(
            [(null, "The skill:"), ("markdown", "# Commit\n\n```\nfeat(mcp): x\n```\n\nEnd of skill."), (null, "Approve it?")],
            AnswerPieces.Of(answer));
    }

    [Fact]
    public void ClosesOnlyOnTheSameCharacter()
    {
        Assert.Equal(
            [("text", "~~~\nstill code"), (null, "after")],
            AnswerPieces.Of("```text\n~~~\nstill code\n```\nafter"));
        Assert.Equal(
            [("", "```\nstill code")],
            AnswerPieces.Of("~~~\n```\nstill code\n~~~"));
    }

    [Fact]
    public void ClosesOnAFenceAtLeastAsLongButNotOneWithWords()
    {
        Assert.Equal(
            [("", "```js\ncode"), (null, "after")],
            AnswerPieces.Of("```\n```js\ncode\n`````\nafter"));
    }

    [Fact]
    public void TakesInlineCodeOnItsOwnLineForProse()
    {
        Assert.Equal([(null, "```a`b```")], AnswerPieces.Of("```a`b```"));
    }

    [Fact]
    public void LeavesAnUnfinishedBlockOpenWhileTheAnswerStreams()
    {
        Assert.Equal(
            [(null, "Here:"), ("markdown", "# Commit\n\n```\nfeat: x\n```")],
            AnswerPieces.Of("Here:\n\n````markdown\n# Commit\n\n```\nfeat: x\n```\n"));
    }
}
