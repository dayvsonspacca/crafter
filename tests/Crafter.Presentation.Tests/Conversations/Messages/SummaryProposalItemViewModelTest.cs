using Crafter.Presentation.Conversations.Messages;

namespace Crafter.Presentation.Tests.Conversations.Messages;

public sealed class SummaryProposalItemViewModelTest
{
    [Fact]
    public void ShowsTheSummaryDrawnAndItsChangesOnDemand()
    {
        var proposal = new SummaryProposalItemViewModel("Agent", "Crafter", "Crafter is my IDE.", "# Crafter\n\nCrafter is my IDE.");

        Assert.True(proposal.ShowsPreview);
        Assert.Equal("# Crafter\n\nCrafter is my IDE.", proposal.Proposed);

        proposal.ShowChangesCommand.Execute(null);

        Assert.True(proposal.ShowsChanges);
        Assert.False(proposal.ShowsPreview);
        Assert.Contains(proposal.Lines, line => line.IsAdded && line.Marked == "+ # Crafter");

        proposal.ShowPreviewCommand.Execute(null);

        Assert.True(proposal.ShowsPreview);
    }
}
