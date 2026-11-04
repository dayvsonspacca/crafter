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

    [Fact]
    public void ComesWithoutPicturesByDefault()
    {
        Assert.Empty(new Prompt("oi").Pictures);
    }

    [Fact]
    public void EqualsAnotherWithTheSamePictures()
    {
        Assert.Equal(new Prompt("[Image #1]", [new Picture("image/png", [1, 2])]), new Prompt("[Image #1]", [new Picture("image/png", [1, 2])]));
        Assert.NotEqual(new Prompt("[Image #1]", [new Picture("image/png", [1, 2])]), new Prompt("[Image #1]", [new Picture("image/png", [3])]));
    }

    [Fact]
    public void RefusesAnEmptyPicture()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Picture("image/png", []));
    }
}
