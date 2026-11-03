using Crafter.Core.Projects;
using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Tests.Projects;

public sealed class ProjectNamerTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-namer-").FullName;

    [Fact]
    public async Task PreviewsTheChosenIconBeforeSaving()
    {
        var namer = new ProjectNamer(new ImagePicker("/home/hero/Pictures/anvil.png"));

        var naming = namer.Add(folder, new ProjectName("crafter"));
        await namer.ChooseIconCommand.ExecuteAsync(null);

        Assert.Equal("/home/hero/Pictures/anvil.png", namer.PreviewFile);
        Assert.True(namer.HasPreview);

        namer.Finish();

        Assert.Equal(new Named(new ProjectName("crafter"), new NewIcon("/home/hero/Pictures/anvil.png")), await naming);
    }

    [Fact]
    public void ShowsTheIconAProjectAlreadyHas()
    {
        var icon = Path.Combine(folder, ".crafter", "icon.png");
        Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
        File.WriteAllBytes(icon, [0]);
        var namer = new ProjectNamer(new ImagePicker(null));

        _ = namer.Edit(new Project(folder, new ProjectName("crafter"), ".crafter/icon.png"));

        Assert.Equal(icon, namer.PreviewFile);
    }

    [Fact]
    public void ShowsTheLetterWhenTheIconIsGone()
    {
        var namer = new ProjectNamer(new ImagePicker(null));

        _ = namer.Edit(new Project(folder, new ProjectName("crafter"), ".crafter/icon.png"));

        Assert.False(namer.HasPreview);
        Assert.Equal("C", namer.Letter);
    }

    [Fact]
    public async Task DropsTheIconWhenRemoved()
    {
        var namer = new ProjectNamer(new ImagePicker(null));

        var naming = namer.Edit(new Project(folder, new ProjectName("crafter")));
        namer.RemoveIconCommand.Execute(null);
        namer.Finish();

        Assert.Equal(new Named(new ProjectName("crafter"), new NoIcon()), await naming);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
