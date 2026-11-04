namespace Crafter.Markdown.Tests;

public sealed class ProseTest
{
    [Fact]
    public void ReadsParagraphsAndHeadings()
    {
        var blocks = Prose.Parse("# Title\n\nFirst paragraph.\n\nSecond one.");

        Assert.Equal(3, blocks.Count);
        Assert.Equal((1, "Title"), blocks[0] is Heading heading ? (heading.Level, Text(heading.Fragments)) : default);
        Assert.Equal("First paragraph.", Text(Assert.IsType<Paragraph>(blocks[1]).Fragments));
        Assert.Equal("Second one.", Text(Assert.IsType<Paragraph>(blocks[2]).Fragments));
    }

    [Fact]
    public void FlattensInlineStylesIntoFragments()
    {
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(Prose.Parse("Plain **bold** *italic* ~~gone~~ `code` ***both***")));

        Assert.Equal(
            [
                new Fragment("Plain "), new Fragment("bold", Look.Bold), new Fragment(" "), new Fragment("italic", Look.Italic),
                new Fragment(" "), new Fragment("gone", Look.Strike), new Fragment(" "), new Fragment("code", Look.Code),
                new Fragment(" "), new Fragment("both", Look.Bold | Look.Italic),
            ],
            paragraph.Fragments);
    }

    [Fact]
    public void KeepsLinksAndAutolinks()
    {
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(Prose.Parse("See [the docs](https://example.com/docs) or https://crafter.dev")));

        Assert.Equal(
            [
                new Fragment("See "), new Fragment("the docs", Link: "https://example.com/docs"), new Fragment(" or "),
                new Fragment("https://crafter.dev", Link: "https://crafter.dev"),
            ],
            paragraph.Fragments);
    }

    [Fact]
    public void TurnsSoftBreaksIntoSpacesAndHardBreaksIntoNewLines()
    {
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(Prose.Parse("one\ntwo  \nthree")));

        Assert.Equal("one two\nthree", Text(paragraph.Fragments));
    }

    [Fact]
    public void ReadsBulletAndNumberedListsWithNesting()
    {
        var blocks = Prose.Parse("- Olá, bão?\n- Outer\n  - Inner\n\n3. three\n4. four");

        var bullets = Assert.IsType<ItemList>(blocks[0]);
        Assert.False(bullets.Ordered);
        Assert.Equal("Olá, bão?", Text(Assert.IsType<Paragraph>(bullets.Items[0].Blocks[0]).Fragments));
        var nested = Assert.IsType<ItemList>(bullets.Items[1].Blocks[1]);
        Assert.Equal("Inner", Text(Assert.IsType<Paragraph>(Assert.Single(nested.Items).Blocks[0]).Fragments));

        var numbered = Assert.IsType<ItemList>(blocks[1]);
        Assert.True(numbered.Ordered);
        Assert.Equal(3, numbered.Start);
        Assert.Equal(2, numbered.Items.Count);
    }

    [Fact]
    public void ReadsTaskItemsWithoutTheBoxInTheirText()
    {
        var list = Assert.IsType<ItemList>(Assert.Single(Prose.Parse("- [x] done\n- [ ] open")));

        Assert.Equal([true, false], list.Items.Select(item => item.Checked));
        Assert.Equal("done", Text(Assert.IsType<Paragraph>(list.Items[0].Blocks[0]).Fragments));
    }

    [Fact]
    public void ReadsTablesWithTheirHeaderAndAlignment()
    {
        var table = Assert.IsType<Table>(Assert.Single(Prose.Parse("| Flow | State |\n|:---|---:|\n| **Evolve** | open |\n| Intranet | `9` tables |")));

        Assert.Equal([Alignment.Start, Alignment.End], table.Columns);
        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal(["Flow", "State"], table.Rows[0].Cells.Select(Text));
        Assert.Equal(["Evolve", "open"], table.Rows[1].Cells.Select(Text));
        Assert.Equal([new Fragment("9", Look.Code), new Fragment(" tables")], table.Rows[2].Cells[1]);
    }

    [Fact]
    public void ReadsQuotesCodeAndRules()
    {
        var blocks = Prose.Parse("> quoted\n\n    indented code\n\n---\n\n~~~sql\nselect 1;\n~~~");

        Assert.Equal("quoted", Text(Assert.IsType<Paragraph>(Assert.Single(Assert.IsType<Quote>(blocks[0]).Blocks)).Fragments));
        Assert.Equal(new Code(null, "indented code"), blocks[1]);
        Assert.IsType<Rule>(blocks[2]);
        Assert.Equal(new Code("sql", "select 1;"), blocks[3]);
    }

    private static string Text(IReadOnlyList<Fragment> fragments) => string.Concat(fragments.Select(fragment => fragment.Text));
}
