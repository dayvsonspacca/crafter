using Crafter.Core.Agents;

namespace Crafter.Core.Tests.Agents;

public sealed class AgentIdTest
{
    [Fact]
    public void KeepsItsValue()
    {
        Assert.Equal("claude-code", new AgentId("claude-code").ToString());
    }

    [Fact]
    public void RefusesABlankValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AgentId(" "));
    }
}
