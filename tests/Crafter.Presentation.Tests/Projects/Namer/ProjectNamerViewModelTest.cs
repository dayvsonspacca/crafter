using Crafter.Core.Projects;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Tests.Fakes;

namespace Crafter.Presentation.Tests.Projects.Namer;

public sealed class ProjectNamerViewModelTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-namer-").FullName;

    [Fact]
    public async Task PreviewsTheChosenIconBeforeSaving()
    {
        var namer = new ProjectNamerViewModel(new ScriptedPicker("/home/hero/Pictures/anvil.png"));

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
        var namer = new ProjectNamerViewModel(new ScriptedPicker(null));

        _ = namer.Edit(new Project(folder, new ProjectName("crafter"), ".crafter/icon.png"));

        Assert.Equal(icon, namer.PreviewFile);
    }

    [Fact]
    public void ShowsTheLetterWhenTheIconIsGone()
    {
        var namer = new ProjectNamerViewModel(new ScriptedPicker(null));

        _ = namer.Edit(new Project(folder, new ProjectName("crafter"), ".crafter/icon.png"));

        Assert.False(namer.HasPreview);
        Assert.Equal("C", namer.Letter);
    }

    [Fact]
    public async Task DropsTheIconWhenRemoved()
    {
        var namer = new ProjectNamerViewModel(new ScriptedPicker(null));

        var naming = namer.Edit(new Project(folder, new ProjectName("crafter")));
        namer.RemoveIconCommand.Execute(null);
        namer.Finish();

        Assert.Equal(new Named(new ProjectName("crafter"), new NoIcon()), await naming);
    }

    [Fact]
    public async Task GivesTheSummaryOnlyWhenItChanged()
    {
        var namer = new ProjectNamerViewModel(new ScriptedPicker(null));

        var unchanged = namer.Edit(new Project(folder, new ProjectName("crafter")), "Crafter is my IDE.");
        namer.Summary = "Crafter is my IDE.  ";
        namer.Finish();
        Assert.Null(((Named)await unchanged).Summary);

        var changed = namer.Edit(new Project(folder, new ProjectName("crafter")), "Crafter is my IDE.");
        namer.Summary = "Crafter is my IDE, in C#.";
        namer.Finish();
        Assert.Equal("Crafter is my IDE, in C#.", ((Named)await changed).Summary);
    }

    [Fact]
    public async Task AddingAProjectAsksForNoSummary()
    {
        var namer = new ProjectNamerViewModel(new ScriptedPicker(null));

        var naming = namer.Add(folder, new ProjectName("crafter"));
        namer.Summary = "Crafter is my IDE.";
        namer.Finish();

        Assert.Null(((Named)await naming).Summary);
        Assert.False(namer.CanInherit);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
