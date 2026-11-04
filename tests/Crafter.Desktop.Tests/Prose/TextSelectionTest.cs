using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Crafter.Desktop.Prose;

namespace Crafter.Desktop.Tests.Prose;

public sealed class TextSelectionTest
{
    [Fact]
    public void CopiesOnlyTheWordsSelectedInAListItem()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("- Olá, bão?\n- Segundo item");

            chat.Drag(chat.Before("bão"), chat.After("bão"));

            Assert.Equal("bão", chat.Copied());
        });
    }

    [Fact]
    public void CopiesTheMarkerWhenTheSelectionStartsOnIt()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("- Olá, bão?\n- Segundo item");

            chat.Drag(chat.LeftOf(chat.Marker("Olá")), chat.After("bão?"));

            Assert.Equal("• Olá, bão?", chat.Copied());
        });
    }

    [Fact]
    public void CopiesItemsAsLinesAndKeepsTheMarkersItCovers()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("- Olá, bão?\n- Segundo item\n\n1. um\n2. dois");

            chat.Drag(chat.Before("bão"), chat.After("dois"));

            Assert.Equal("bão?\n• Segundo item\n\n1. um\n2. dois", chat.Copied());
        });
    }

    [Fact]
    public void CopiesAcrossBoldAndPlainTextExactly()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("1. **Duplo clique seleciona a palavra**, copiando com Ctrl+C.\n2. **Copiar automaticamente.** É prático.");

            chat.Drag(chat.Before("clique"), chat.After("É prático"));

            Assert.Equal("clique seleciona a palavra, copiando com Ctrl+C.\n2. Copiar automaticamente. É prático", chat.Copied());
        });
    }

    [Fact]
    public void CopiesParagraphsWithABlankLineBetweenAndNoneAtTheEnd()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("First paragraph here.\n\nSecond paragraph here.");

            chat.Drag(chat.Before("paragraph here.", "First"), chat.After("Second"));

            Assert.Equal("paragraph here.\n\nSecond", chat.Copied());
        });
    }

    [Fact]
    public void SelectsBackwardsTheSameAsForwards()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("First paragraph here.\n\nSecond paragraph here.");

            chat.Drag(chat.After("Second"), chat.Before("paragraph here.", "First"));

            Assert.Equal("paragraph here.\n\nSecond", chat.Copied());
        });
    }

    [Fact]
    public void CopiesTableCellsWithTabsAndRowsAsLines()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("| Flow | State |\n|---|---|\n| Evolve | open |\n| Intranet | mapped |");

            chat.Drag(chat.Before("Flow"), chat.After("open"));

            Assert.Equal("Flow\tState\nEvolve\topen", chat.Copied());
        });
    }

    [Fact]
    public void CopiesAcrossTwoAnswers()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("The first answer.", "The second answer.");

            chat.Drag(chat.Before("first"), chat.After("second"));

            Assert.Equal("first answer.\n\nThe second", chat.Copied());
        });
    }

    [Fact]
    public void CopiesAcrossAPromptAnAnswerAndCode()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.With(
                new ProseText { Text = "Fix the table" },
                new ProseView { Markdown = "Done, see:" },
                new ProseText { Text = "var table = new Table();\nreturn table;" });

            chat.Drag(chat.Before("the table"), chat.After("return"));

            Assert.Equal("the table\n\nDone, see:\n\nvar table = new Table();\nreturn", chat.Copied());
        });
    }

    [Fact]
    public void DoubleClickSelectsTheWordAndTripleClickTheBlock()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("Escolha a opção, por favor.\n\nOutro parágrafo.");

            chat.Click(chat.Before("opção"), times: 2);
            Assert.Equal("opção", chat.Copied());

            chat.Click(chat.Before("opção"), times: 3);
            Assert.Equal("Escolha a opção, por favor.", chat.Copied());
        });
    }

    [Fact]
    public void ShiftClickExtendsTheSelection()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("Alpha beta gamma delta.");

            chat.Click(chat.Before("beta"));
            chat.Click(chat.After("gamma"), modifiers: RawInputModifiers.Shift);

            Assert.Equal("beta gamma", chat.Copied());
        });
    }

    [Fact]
    public void SelectAllTakesEveryBlock()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("# Title\n\nBody text.", "- one\n- two");

            chat.Click(chat.Before("Body"), times: 2);
            chat.Press(PhysicalKey.A);

            Assert.Equal("Title\n\nBody text.\n\n• one\n• two", chat.Copied());
        });
    }

    [Fact]
    public void AClickOrEscapeClearsTheSelectionAndLeavesTheClipboardAlone()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("Alpha beta gamma.");
            chat.Paste("before");

            chat.Drag(chat.Before("beta"), chat.After("gamma"));
            chat.Click(chat.Before("Alpha"));
            Assert.Equal("before", chat.Copied());
            Assert.False(chat.Selection.HasSelection);

            chat.Drag(chat.Before("beta"), chat.After("gamma"));
            chat.Press(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.False(chat.Selection.HasSelection);
        });
    }

    [Fact]
    public void PressesOnButtonsStayTheirs()
    {
        TestApp.Run(() =>
        {
            var clicked = 0;
            var button = new Button { Content = "Retry" };
            button.Click += (_, _) => clicked++;
            var chat = Chat.With(new ProseView { Markdown = "Some answer." }, button);

            chat.Click(button.TranslatePoint(new Point(10, 10), chat.Window)!.Value);

            Assert.Equal(1, clicked);
            Assert.False(chat.Selection.HasSelection);
        });
    }

    [Fact]
    public void DrawsTheSelectionOnEveryBlockItCovers()
    {
        TestApp.Run(() =>
        {
            var chat = Chat.Of("- Olá, bão?\n- Segundo item");

            chat.Drag(chat.Before("bão"), chat.After("Segundo"));

            Assert.Equal((5, 9), Span(chat.Block("Olá")));
            Assert.Equal((0, 1), Span(chat.Marker("Segundo")));
            Assert.Equal((0, 7), Span(chat.Block("Segundo")));
            Assert.Equal((0, 0), Span(chat.Marker("Olá")));
        });
    }

    [Fact]
    public void APlainClickLeavesTheFocusAndASelectionTakesIt()
    {
        TestApp.Run(() =>
        {
            var box = new TextBox { Text = "draft" };
            var chat = Chat.With(box, new ProseView { Markdown = "Alpha beta gamma." });
            box.Focus();

            chat.Click(chat.Before("beta"));
            Assert.True(box.IsFocused);

            chat.Drag(chat.Before("beta"), chat.After("gamma"));
            Assert.True(chat.Scroller.IsFocused);
            Assert.Equal("beta gamma", chat.Copied());
        });
    }

    [Theory]
    [InlineData("Olá, bão?", 5, "bão")]
    [InlineData("Olá, bão?", 7, "bão")]
    [InlineData("Olá, bão?", 8, "?")]
    [InlineData("a  b", 1, "  ")]
    [InlineData("x.y", 1, ".")]
    public void FindsTheWordACharacterBelongsTo(string text, int character, string word)
    {
        var (start, end) = TextSelection.WordAround(text, character)!.Value;

        Assert.Equal(word, text[start..end]);
    }

    private static (int, int) Span(SelectableTextBlock block) => (block.SelectionStart, block.SelectionEnd);
}
