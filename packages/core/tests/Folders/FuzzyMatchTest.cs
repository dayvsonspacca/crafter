using Crafter.Core.Folders;

namespace Crafter.Core.Tests.Folders;

public sealed class FuzzyMatchTest
{
    [Fact]
    public void FindsTheLettersInOrderIgnoringCase()
    {
        Assert.Equal([0, 1, 5], FuzzyMatch.Hits("crafter", "CRE"));
    }

    [Fact]
    public void MatchesEverythingWithoutLetters()
    {
        Assert.Equal([], FuzzyMatch.Hits("crafter", string.Empty));
    }

    [Fact]
    public void MissesWhenALetterIsAbsent()
    {
        Assert.Null(FuzzyMatch.Hits("crafter", "crz"));
    }

    [Fact]
    public void MissesWhenTheLettersAreOutOfOrder()
    {
        Assert.Null(FuzzyMatch.Hits("crafter", "rc"));
    }
}
