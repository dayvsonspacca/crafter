using Crafter.Core.Conversations.Replies;
using Crafter.Presentation.Conversations.Messages;

namespace Crafter.Presentation.Tests.Conversations.Messages;

public sealed class ActivityItemViewModelTest
{
    [Fact]
    public void SplitsAnEditsLineCountsOnceItWentThrough()
    {
        var edit = new ActivityItemViewModel(new Acting("t-1", "Edit", "src/App.cs", "+12 −3"));
        Assert.False(edit.ShowsChanges);

        edit.Finish(new Acted("t-1", Failed: false, Output: string.Empty));

        Assert.True(edit.ShowsChanges);
        Assert.False(edit.ShowsResult);
        Assert.Equal("+12", edit.Added);
        Assert.Equal("−3", edit.Removed);
    }

    [Fact]
    public void ShowsOnlyTheAddedLinesOfANewFile()
    {
        var write = new ActivityItemViewModel(new Acting("t-1", "Write", "README.md", "+40"));
        write.Finish(new Acted("t-1", Failed: false, Output: string.Empty));

        Assert.Equal("+40", write.Added);
        Assert.Null(write.Removed);
    }

    [Fact]
    public void KeepsTheWordsForAFailedEditOrACommand()
    {
        var failed = new ActivityItemViewModel(new Acting("t-1", "Edit", "src/App.cs", "+1 −1"));
        failed.Finish(new Acted("t-1", Failed: true, Output: "no match"));
        Assert.True(failed.ShowsResult);
        Assert.Equal("failed", failed.Result);

        var bash = new ActivityItemViewModel(new Acting("t-2", "Bash", "ls"));
        bash.Finish(new Acted("t-2", Failed: false, Output: "a\nb"));
        Assert.False(bash.ShowsChanges);
        Assert.Equal("2 lines", bash.Result);
    }
}
