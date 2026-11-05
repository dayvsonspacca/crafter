using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Presentation.Conversations.Composing;

namespace Crafter.Presentation.Tests.Conversations.Composing;

public sealed class AttachmentsTest
{
    private readonly Attachments attachments = new();

    [Fact]
    public void LabelsAPasteWithManyLines()
    {
        Assert.Equal("[Pasted text #1 +9 lines]", attachments.Paste(string.Join("\r\n", Enumerable.Repeat("x", 10))));
    }

    [Fact]
    public void LabelsALongSingleLinePaste()
    {
        Assert.Equal("[Pasted text #1]", attachments.Paste(new string('x', Attachments.LongestPaste + 1)));
    }

    [Fact]
    public void NumbersPastesOneAfterAnother()
    {
        attachments.Paste(new string('x', Attachments.LongestPaste + 1));

        Assert.Equal("[Pasted text #2]", attachments.Paste(new string('y', Attachments.LongestPaste + 1)));
    }

    [Fact]
    public void LeavesALabelItDoesNotKnowAlone()
    {
        Assert.Equal("see [Pasted text #7 +3 lines]", attachments.Compose("see [Pasted text #7 +3 lines]")?.Text);
    }

    [Fact]
    public void ComposesNothingFromABlankDraft()
    {
        Assert.Null(attachments.Compose("   "));
    }

    [Fact]
    public void SendsFilesAsReferencesAfterTheWords()
    {
        attachments.Attach("@docs/plan.pdf");
        attachments.Attach("@\"/tmp/my notes.txt\"");

        Assert.Equal("Read these\n\n@docs/plan.pdf @\"/tmp/my notes.txt\"", attachments.Compose("Read these")?.Text);
    }

    [Fact]
    public void SendsTodosAsALineEachAfterTheFiles()
    {
        attachments.Attach("@docs/plan.pdf");
        attachments.Attach(new Todo(3, "Draw the panel"));
        attachments.Attach(new Todo(5, "Cite a todo"));

        Assert.Equal("Do these\n\n@docs/plan.pdf\n\nTodo #3: Draw the panel\nTodo #5: Cite a todo", attachments.Compose("Do these")?.Text);
    }

    [Fact]
    public void SendsATodoWithoutWordsAsItsLine()
    {
        attachments.Attach(new Todo(3, "Draw the panel"));

        Assert.Equal("Todo #3: Draw the panel", attachments.Compose(" ")?.Text);
    }

    [Fact]
    public void SendsFilesWithoutWordsAsTheirReferences()
    {
        attachments.Attach(new Picture("image/png", [1]));
        attachments.Attach("@docs/plan.pdf");

        var prompt = attachments.Compose(" ");

        Assert.Equal("@docs/plan.pdf", prompt?.Text);
        Assert.Single(prompt!.Pictures);
    }

    [Fact]
    public void ForgetsADetachedFile()
    {
        attachments.Attach("@docs/plan.pdf");
        attachments.Detach("@docs/plan.pdf");

        Assert.Null(attachments.Compose(string.Empty));
    }

    [Fact]
    public void ForgetsEverythingOnceCleared()
    {
        attachments.Attach(new Picture("image/png", [1]));
        attachments.Attach("@docs/plan.pdf");
        attachments.Clear();

        Assert.Null(attachments.Compose(string.Empty));
    }
}
