using Crafter.Core.Projects;
using Crafter.Presentation.Projects;
using Crafter.Presentation.Tests.Fakes;

namespace Crafter.Presentation.Tests.Projects;

public sealed class ProjectDetailsViewModelTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-details-").FullName;

    private readonly EditorInMemory editor = new();

    private Project Crafter => new(folder, new ProjectName("Crafter"));

    [Fact]
    public async Task PreviewsTheChosenIconAndKeepsItOnlyOnceSaved()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker("/home/hero/Pictures/anvil.png"));
        Assert.False(details.IsChanged);

        await details.ChooseIconCommand.ExecuteAsync(null);

        Assert.Equal("/home/hero/Pictures/anvil.png", details.PreviewFile);
        Assert.True(details.IsChanged);
        Assert.Empty(editor.Saved);

        details.SaveCommand.Execute(null);

        Assert.Equal([(new ProjectName("Crafter"), (IconChange)new NewIcon("/home/hero/Pictures/anvil.png"), (string?)null)], editor.Saved);
        Assert.False(details.IsChanged);
    }

    [Fact]
    public void ShowsTheIconAProjectAlreadyHas()
    {
        var icon = Path.Combine(folder, ".crafter", "icon.png");
        Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
        File.WriteAllBytes(icon, [0]);

        var details = new ProjectDetailsViewModel(new Project(folder, new ProjectName("Crafter"), ".crafter/icon.png"), editor, new ScriptedPicker(null));

        Assert.Equal(icon, details.PreviewFile);
    }

    [Fact]
    public void ShowsTheLetterWhenTheIconIsGone()
    {
        var details = new ProjectDetailsViewModel(new Project(folder, new ProjectName("crafter"), ".crafter/icon.png"), editor, new ScriptedPicker(null));

        Assert.False(details.HasPreview);
        Assert.Equal("C", details.Letter);
    }

    [Fact]
    public void DropsTheIconWhenRemoved()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        details.RemoveIconCommand.Execute(null);
        details.SaveCommand.Execute(null);

        Assert.IsType<NoIcon>(Assert.Single(editor.Saved).Icon);
    }

    [Fact]
    public void SavesTheSummaryOnlyWhenItChanged()
    {
        editor.Summaries[Crafter.Folder] = "Crafter is my IDE.";
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        details.Name = "Crafter IDE";
        details.Summary = "Crafter is my IDE.  ";
        details.SaveCommand.Execute(null);
        Assert.Null(editor.Saved[^1].Summary);

        details.Summary = "Crafter is my IDE, in C#.";
        details.SaveCommand.Execute(null);
        Assert.Equal("Crafter is my IDE, in C#.", editor.Saved[^1].Summary);
    }

    [Fact]
    public void SavesNothingUnchangedOrNameless()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));
        Assert.False(details.SaveCommand.CanExecute(null));
        Assert.False(details.DiscardCommand.CanExecute(null));

        details.Name = " ";

        Assert.True(details.IsChanged);
        Assert.False(details.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DiscardingGoesBackToWhatIsSaved()
    {
        editor.Summaries[Crafter.Folder] = "Crafter is my IDE.";
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        details.Name = "Something";
        details.Summary = "Something else";
        details.RemoveIconCommand.Execute(null);
        details.DiscardCommand.Execute(null);

        Assert.Equal("Crafter", details.Name);
        Assert.Equal("Crafter is my IDE.", details.Summary);
        Assert.False(details.IsChanged);
    }

    [Fact]
    public void TakesASummarySavedElsewhereUnlessItHoldsAnEdit()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        editor.Summaries[Crafter.Folder] = "Crafter is my IDE.";
        details.Describe(Crafter);
        Assert.Equal("Crafter is my IDE.", details.Summary);

        details.Summary = "My own words";
        editor.Summaries[Crafter.Folder] = "The agent's words";
        details.Describe(Crafter);

        Assert.Equal("My own words", details.Summary);
        Assert.True(details.IsChanged);
    }

    [Fact]
    public void StartsTheSummaryFromTheInstructionsFileTheProjectBrings()
    {
        editor.Instructions = new AgentInstructions("CLAUDE.md", "Crafter is my IDE.");
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        Assert.True(details.CanInherit);
        Assert.Equal("Start from CLAUDE.md", details.InheritLabel);

        details.InheritCommand.Execute(null);

        Assert.Equal("Crafter is my IDE.", details.Summary);
        Assert.False(details.CanInherit);
        Assert.Empty(editor.Saved);
    }

    [Fact]
    public void OffersNoInstructionsFileToAProjectWithoutOne()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));

        Assert.False(details.CanInherit);
        Assert.False(details.InheritCommand.CanExecute(null));
    }

    [Fact]
    public void PreviewsTheSummaryWithoutTouchingIt()
    {
        var details = new ProjectDetailsViewModel(Crafter, editor, new ScriptedPicker(null));
        Assert.True(details.IsWriting);

        details.Summary = "# Crafter";
        details.PreviewCommand.Execute(null);

        Assert.True(details.IsPreviewing);
        Assert.False(details.IsWriting);
        Assert.Equal("# Crafter", details.Summary);

        details.WriteCommand.Execute(null);

        Assert.True(details.IsWriting);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
