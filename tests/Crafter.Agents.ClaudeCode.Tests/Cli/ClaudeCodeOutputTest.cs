using Crafter.Agents.ClaudeCode.Cli;

namespace Crafter.Agents.ClaudeCode.Tests.Cli;

public sealed class ClaudeCodeOutputTest : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"crafter-{Guid.NewGuid():N}.output");

    [Fact]
    public void ReadsAShortOutputWhole()
    {
        File.WriteAllText(file, "one\ntwo\n");

        Assert.Equal("one\ntwo\n", ClaudeCodeOutput.Latest(file));
    }

    [Fact]
    public void ReadsTheEndOfALongOutputFromAWholeLine()
    {
        File.WriteAllText(file, string.Concat(Enumerable.Range(0, 5000).Select(line => $"line {line}\n")));

        var latest = ClaudeCodeOutput.Latest(file)!;

        Assert.StartsWith("line ", latest);
        Assert.EndsWith("line 4999\n", latest);
        Assert.True(latest.Length <= 16 * 1024);
    }

    [Fact]
    public void ReadsNothingFromAFileThatIsGone() => Assert.Null(ClaudeCodeOutput.Latest(file));

    public void Dispose() => File.Delete(file);
}
