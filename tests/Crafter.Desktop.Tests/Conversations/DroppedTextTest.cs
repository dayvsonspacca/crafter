using Crafter.Desktop.Conversations;

namespace Crafter.Desktop.Tests.Conversations;

public sealed class DroppedTextTest
{
    private static readonly HashSet<string> OnDisk = ["/home/hero/install.log", "/home/hero/my notes.txt", "/home/hero/docs"];

    [Fact]
    public void ReadsFileUrisOnePerLine()
    {
        Assert.Equal(
            ["/home/hero/install.log", "/home/hero/my notes.txt"],
            DroppedText.Paths("file:///home/hero/install.log\r\nfile:///home/hero/my%20notes.txt\r\n", OnDisk.Contains));
    }

    [Fact]
    public void ReadsAbsolutePathsAndFolders()
    {
        Assert.Equal(["/home/hero/docs", "/home/hero/install.log"], DroppedText.Paths("/home/hero/docs\n/home/hero/install.log", OnDisk.Contains));
    }

    [Fact]
    public void SkipsCommentLinesOfAUriList()
    {
        Assert.Equal(["/home/hero/install.log"], DroppedText.Paths("# from the file manager\nfile:///home/hero/install.log", OnDisk.Contains));
    }

    [Theory]
    [InlineData("just some words")]
    [InlineData("/home/hero/install.log\nand a sentence")]
    [InlineData("/home/hero/gone.txt")]
    [InlineData("relative/path.txt")]
    [InlineData("https://example.com/file.txt")]
    public void TreatsAnythingElseAsText(string text)
    {
        Assert.Empty(DroppedText.Paths(text, OnDisk.Contains));
    }
}
