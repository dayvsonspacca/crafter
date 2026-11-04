using Crafter.Core.Agents.Options;

namespace Crafter.Core.Tests.Agents.Options;

public sealed class ConfigurationTest
{
    [Fact]
    public void KnowsNothingWhenEmpty()
    {
        Assert.Null(Configuration.Empty["model"]);
    }

    [Fact]
    public void KeepsAValue()
    {
        Assert.Equal("opus", Configuration.Empty.With("model", "opus")["model"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForgetsAValueSetToNothing(string? nothing)
    {
        Assert.Null(Configuration.Empty.With("model", "opus").With("model", nothing)["model"]);
    }

    [Fact]
    public void LiesOverAnotherConfiguration()
    {
        var preferred = Configuration.Empty.With("model", "opus").With("tools", "plan");

        var adjusted = Configuration.Empty.With("tools", "chat").Over(preferred);

        Assert.Equal("opus", adjusted["model"]);
        Assert.Equal("chat", adjusted["tools"]);
    }

    [Fact]
    public void ClearsWhatItMarksAsCleared()
    {
        var preferred = Configuration.Empty.With("model", "opus");

        Assert.Null(Configuration.Empty.With("model", Configuration.Cleared).Over(preferred)["model"]);
    }

    [Fact]
    public void ComparesByWhatItHolds()
    {
        var one = Configuration.Empty.With("model", "opus").With("effort", "high");
        var other = Configuration.Empty.With("effort", "high").With("model", "opus");

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.True(one.Equals((object)other));
        Assert.NotEqual(one, Configuration.Empty.With("model", "opus"));
        Assert.NotEqual(one, Configuration.Empty.With("model", "sonnet").With("effort", "high"));
        Assert.False(one.Equals(null));
    }
}
