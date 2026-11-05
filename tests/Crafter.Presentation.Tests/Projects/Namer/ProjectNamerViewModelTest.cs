using Crafter.Core.Projects;
using Crafter.Presentation.Projects.Namer;

namespace Crafter.Presentation.Tests.Projects.Namer;

public sealed class ProjectNamerViewModelTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-namer-").FullName;

    [Fact]
    public async Task NamesTheFolderItAdds()
    {
        var namer = new ProjectNamerViewModel();

        var naming = namer.Add(folder, new ProjectName("crafter"));
        Assert.True(namer.IsOpen);
        Assert.Equal("crafter", namer.Name);

        namer.Name = "Crafter";
        namer.Finish();

        Assert.Equal(new Named(new ProjectName("Crafter")), await naming);
        Assert.False(namer.IsOpen);
    }

    [Fact]
    public async Task GoesBackToTheFolders()
    {
        var namer = new ProjectNamerViewModel();

        var naming = namer.Add(folder, new ProjectName("crafter"));
        namer.Back();

        Assert.IsType<WentBack>(await naming);
    }

    [Fact]
    public void AddsNothingWithoutAName()
    {
        var namer = new ProjectNamerViewModel();

        _ = namer.Add(folder, new ProjectName("crafter"));
        namer.Name = "  ";

        Assert.False(namer.FinishCommand.CanExecute(null));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
