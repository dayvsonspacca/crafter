using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Projects;

public sealed class ProjectTest
{
    [Fact]
    public void IsNamedAfterItsFolder()
    {
        Assert.Equal(new ProjectName("crafter"), new Project("/home/hero/Projetos/me/desktop/crafter").Name);
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
        Assert.Equal(new ProjectName("/"), new Project("/").Name);
    }

    [Fact]
    public void KeepsTheNameItWasGiven()
    {
        Assert.Equal("Crafter", new Project("/home/hero/crafter", new ProjectName("Crafter")).Name.Text);
    }

    [Fact]
    public void IsTheSameProjectWhateverItIsCalled()
    {
        var named = new Project("/home/hero/crafter", new ProjectName("Crafter"));

        Assert.Equal(new Project("/home/hero/crafter"), named);
        Assert.Equal(new Project("/home/hero/crafter").GetHashCode(), named.GetHashCode());
        Assert.NotEqual(new Project("/home/hero/beacon"), named);
        Assert.False(named.Equals(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RefusesABlankFolder(string folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Project(folder));
    }
}
