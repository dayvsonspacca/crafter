using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

public sealed class ProjectTest
{
    [Fact]
    public void IsNamedAfterItsFolder()
    {
        Assert.Equal("crafter", new Project("/home/hero/Projetos/me/desktop/crafter").Name);
    }

    [Fact]
    public void ForgetsTheTrailingSeparator()
    {
        Assert.Equal(new Project("/home/hero/beacon"), new Project("/home/hero/beacon/"));
    }

    [Fact]
    public void ResolvesARelativeFolder()
    {
        Assert.Equal(Path.GetFullPath("beacon"), new Project("beacon").Folder);
    }

    [Fact]
    public void IsNamedAfterTheRootWhenItIsTheRoot()
    {
        Assert.Equal("/", new Project("/").Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RefusesABlankFolder(string folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Project(folder));
    }
}
