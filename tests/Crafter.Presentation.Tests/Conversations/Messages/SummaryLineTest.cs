using Crafter.Presentation.Conversations.Messages;

namespace Crafter.Presentation.Tests.Conversations.Messages;

public sealed class SummaryLineTest
{
    [Fact]
    public void AFirstSummaryIsAllAdded()
    {
        Assert.Equal(["+ Crafter is my IDE.", "+ It is written in C#."], SummaryLine.Between(null, "Crafter is my IDE.\nIt is written in C#.").Select(line => line.Marked));
    }

    [Fact]
    public void KeepsTheLinesBothShareAndMarksTheRest()
    {
        var lines = SummaryLine.Between(
            "Crafter is my IDE.\nIt is written in C#.\nTests run with dotnet test.",
            "Crafter is my IDE.\nIt is written in C# with Avalonia.\nTests run with dotnet test.\nIt publishes to dist.");

        Assert.Equal(
            [
                "  Crafter is my IDE.",
                "- It is written in C#.",
                "+ It is written in C# with Avalonia.",
                "  Tests run with dotnet test.",
                "+ It publishes to dist.",
            ],
            lines.Select(line => line.Marked));
        Assert.True(lines[1].IsRemoved);
        Assert.True(lines[2].IsAdded);
    }
}
