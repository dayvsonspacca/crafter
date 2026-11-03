using Crafter.Core.Conversations;

namespace Crafter.Core.Tests.Conversations;

public sealed class ChatTitleTest
{
    [Fact]
    public void StartsUntitled()
    {
        Assert.Equal("New chat", ChatTitle.Untitled.Text);
    }

    [Fact]
    public void TakesTheFirstLineOfThePrompt()
    {
        Assert.Equal("Fix reconnect after TimedOut", ChatTitle.From(new Prompt("  Fix reconnect after TimedOut\nThe client hangs.")).Text);
    }

    [Fact]
    public void CutsALongPrompt()
    {
        var title = ChatTitle.From(new Prompt("Investigate why the bookshelf forgets the projects after a restart"));

        Assert.Equal("Investigate why the bookshelf forgets t…", title.Text);
        Assert.Equal(40, title.Text.Length);
    }

    [Fact]
    public void KeepsAGivenTitle()
    {
        Assert.Equal("Cover Paces", ChatTitle.Of(" Cover Paces ").ToString());
    }

    [Fact]
    public void RefusesABlankTitle()
    {
        Assert.ThrowsAny<ArgumentException>(() => ChatTitle.Of(" "));
    }
}
