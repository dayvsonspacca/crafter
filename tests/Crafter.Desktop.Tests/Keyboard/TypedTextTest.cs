using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Crafter.Desktop.Keyboard;

namespace Crafter.Desktop.Tests.Keyboard;

public sealed class TypedTextTest
{
    [Fact]
    public void KeepsAnEscapeSentAsTextOutOfTheField()
    {
        TestApp.Run(() =>
        {
            var (window, box) = Field();

            window.KeyTextInput("isso aparecer -> ");
            window.KeyTextInput("\u001b");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("isso aparecer -> ", box.Text);
        });
    }

    [Fact]
    public void DropsControlCharactersAndKeepsTheRest()
    {
        TestApp.Run(() =>
        {
            var (window, box) = Field();

            window.KeyTextInput("a\u001bb\u0007c");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("abc", box.Text);
        });
    }

    [Theory]
    [InlineData("\u001b", "")]
    [InlineData("tab\there", "tab\there")]
    [InlineData("line\r\nbreak", "line\r\nbreak")]
    [InlineData("Olá ✓", "Olá ✓")]
    public void KeepsTabsLineBreaksAndPrintableText(string typed, string kept)
    {
        Assert.Equal(kept, TypedText.Printable(typed));
    }

    private static (Window Window, TextBox Box) Field()
    {
        var box = new TextBox { AcceptsReturn = true };
        var window = new Window { Content = box };
        TypedText.Watch(window);
        window.Show();
        box.Focus();
        Dispatcher.UIThread.RunJobs();

        return (window, box);
    }
}
