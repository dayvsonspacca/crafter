using Crafter.Core.Library;

namespace Crafter.Core.Tests.Library;

public sealed class SkillCallsTest
{
    private static readonly string[] Known = ["commit", "pr-review"];

    [Theory]
    [InlineData("/commit", new[] { "commit" })]
    [InlineData("/commit the staged files", new[] { "commit" })]
    [InlineData("review it with /pr-review, then /commit.", new[] { "pr-review", "commit" })]
    [InlineData("/commit and /commit again", new[] { "commit" })]
    [InlineData("see src/commit and /home/hero/commit", new string[0])]
    [InlineData("/init the project", new string[0])]
    public void FindsTheKnownSkillsATextCalls(string text, string[] called)
    {
        Assert.Equal(called, SkillCalls.In(text, Known));
    }

    [Theory]
    [InlineData("/init", "init")]
    [InlineData("  /review this", "review")]
    [InlineData("/crafter:commit", "crafter:commit")]
    [InlineData("/commit now", null)]
    [InlineData("/home/hero/notes.md says what?", null)]
    [InlineData("explain /init", null)]
    [InlineData("plain words", null)]
    public void NamesACommandTheTextOpensWithThatIsNotASkill(string text, string? stranger)
    {
        Assert.Equal(stranger, SkillCalls.Stranger(text, Known));
    }

    [Fact]
    public void RewritesOnlyTheCalledSkills()
    {
        Assert.Equal(
            "/x:commit then /x:pr-review, not /init or src/commit",
            SkillCalls.Rewrite("/commit then /pr-review, not /init or src/commit", Known, id => $"/x:{id}"));
    }
}
