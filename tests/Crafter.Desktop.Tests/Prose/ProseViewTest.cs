using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Crafter.Desktop.Prose;

namespace Crafter.Desktop.Tests.Prose;

public sealed class ProseViewTest
{
    private const string Written = "Here it is:\n\n```markdown\n# Crafter\n\nAn IDE for **agents**.\n```";

    [Fact]
    public void DrawsTheMarkdownAnAgentWritesOut()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of(Written);

            Assert.True(chat.Block("Crafter").IsEffectivelyVisible);
            Assert.Equal("An IDE for agents.", chat.Block("An IDE").Plain);
            Assert.DoesNotContain(Shown(chat), block => block.Plain.Contains("# Crafter", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ShowsItsSourceOnDemandAndDrawsItAgain()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of(Written);

            Press(chat, "Source");
            Assert.True(chat.Block("# Crafter").IsEffectivelyVisible);
            Assert.False(chat.Block("An IDE for agents.").IsEffectivelyVisible);

            Press(chat, "Preview");
            Assert.False(chat.Block("# Crafter").IsEffectivelyVisible);
            Assert.True(chat.Block("An IDE for agents.").IsEffectivelyVisible);
        });
    }

    [Fact]
    public void CopiesWhatItShows()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of(Written);

            chat.Drag(chat.Before("Crafter"), chat.After("agents."));

            Assert.DoesNotContain("#", chat.Copied());
            Assert.Contains("An IDE for agents.", chat.Copied());
        });
    }

    [Fact]
    public void LeavesOtherCodeAsCode()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("```bash\n# not a heading\nls\n```");

            Assert.True(chat.Block("# not a heading").IsEffectivelyVisible);
            Assert.DoesNotContain(chat.Window.GetVisualDescendants().OfType<Button>(), button => button.Content is "Source");
        });
    }

    private static IEnumerable<ProseText> Shown(Chat chat) =>
        chat.Window.GetVisualDescendants().OfType<ProseText>().Where(block => block.IsEffectivelyVisible);

    private static void Press(Chat chat, string label)
    {
        chat.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Chat.Settle();
    }
}
