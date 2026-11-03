using Crafter.Core.Conversations;

namespace Crafter.Core.Tests.Conversations;

public sealed class PromptTest
{
    [Fact]
    public void KeepsItsText()
    {
        Assert.Equal("Explain the title bar", new Prompt("Explain the title bar").Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesABlankText(string text)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Prompt(text));
    }
}
