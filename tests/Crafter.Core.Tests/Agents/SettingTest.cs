using Crafter.Core.Agents;

namespace Crafter.Core.Tests.Agents;

public sealed class SettingTest
{
    private static readonly Choice Model = new("model", "Model", "The model that answers.", [new(null, "Default"), new("opus", "Opus")]);

    private static readonly Entry Executable = new("executable", "Executable", "The command that starts it.", "claude");

    [Fact]
    public void ChoosesTheFirstOptionWhenNothingIsSet()
    {
        Assert.Equal("Default", Model.Chosen(Configuration.Empty).Label);
    }

    [Fact]
    public void ChoosesTheOptionThatIsSet()
    {
        Assert.Equal("Opus", Model.Chosen(Configuration.Empty.With("model", "opus")).Label);
    }

    [Fact]
    public void ChoosesTheFirstOptionForAnUnknownValue()
    {
        Assert.Equal("Default", Model.Chosen(Configuration.Empty.With("model", "gone")).Label);
    }

    [Fact]
    public void RefusesAChoiceWithoutOptions()
    {
        Assert.Throws<ArgumentException>(() => new Choice("model", "Model", string.Empty, []));
    }

    [Fact]
    public void FallsBackWhenAnEntryIsNotSet()
    {
        Assert.Equal("claude", Executable.Value(Configuration.Empty));
        Assert.Equal("/opt/claude", Executable.Value(Configuration.Empty.With("executable", "/opt/claude")));
    }
}
