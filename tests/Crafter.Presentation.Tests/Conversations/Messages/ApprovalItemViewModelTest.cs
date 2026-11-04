using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;
using Crafter.Presentation.Conversations.Messages;

namespace Crafter.Presentation.Tests.Conversations.Messages;

public sealed class ApprovalItemViewModelTest
{
    [Fact]
    public void SaysWhatAlwaysAllowingKeepsGatheredByHowFarItReaches()
    {
        var approval = new ApprovalItemViewModel(
            new NeedsApproval("r-1", new Tool(ToolKind.Shell, "Bash"), "npm test", Remembers: [new Rule("Bash(npm test:*)", Reach.Project), new Rule("Read", Reach.Everywhere), new Rule("/tmp/out", Reach.Project)]),
            "Agent",
            (_, _) => { });

        Assert.Equal("Always allow Bash(npm test:*), /tmp/out in this project, Read everywhere", approval.AlwaysLabel);
    }

    [Fact]
    public void OffersNoAlwaysWhenNothingWouldBeKept()
    {
        var approval = new ApprovalItemViewModel(new NeedsApproval("r-1", new Tool(ToolKind.Shell, "Bash"), "ls"), "Agent", (_, _) => { });

        Assert.False(approval.CanAlwaysAllow);
        Assert.Equal("Agent wants to use Shell", approval.Asking);
    }

    [Fact]
    public void RemembersWhatItKeptOnceAlwaysAllowed()
    {
        Decision? decided = null;
        var approval = new ApprovalItemViewModel(
            new NeedsApproval("r-1", new Tool(ToolKind.Web, "WebFetch"), "https://example.com", Remembers: [new Rule("WebFetch", Reach.Chat)]),
            "Agent",
            (_, decision) => decided = decision);

        approval.AlwaysAllowCommand.Execute(null);

        Assert.IsType<AlwaysAllow>(decided);
        Assert.Equal("Allowed · remembered WebFetch in this chat", approval.Verdict);
    }
}
