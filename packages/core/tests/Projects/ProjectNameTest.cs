using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

public sealed class ProjectNameTest
{
    [Fact]
    public void ForgetsSurroundingSpaces()
    {
        Assert.Equal("Crafter", new ProjectName("  Crafter ").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RefusesABlankName(string text)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ProjectName(text));
    }

    [Theory]
    [InlineData("/home/hero/crafter", "crafter")]
    [InlineData("/home/hero/crafter/", "crafter")]
    [InlineData("/", "/")]
    public void TakesTheFolderName(string folder, string name)
    {
        Assert.Equal(name, ProjectName.Of(folder).Text);
    }
}
