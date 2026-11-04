using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;

namespace Crafter.Markdown;

// Reads an answer's markdown into the blocks the chat draws.
public static class Prose
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public static IReadOnlyList<Block> Parse(string markdown) => Blocks(Markdig.Markdown.Parse(markdown, Pipeline));

    private static List<Block> Blocks(ContainerBlock container) => [.. container.Select(Read).OfType<Block>()];

    private static Block? Read(MarkdigBlock block) => block switch
    {
        HeadingBlock heading => new Heading(heading.Level, Fragments(heading.Inline)),
        ParagraphBlock paragraph => new Paragraph(Fragments(paragraph.Inline)),
        QuoteBlock quote => new Quote(Blocks(quote)),
        ListBlock list => new ItemList(list.IsOrdered, Start(list), [.. list.OfType<ListItemBlock>().Select(ItemOf)]),
        MarkdigTable table => TableOf(table),
        FencedCodeBlock fenced => new Code(fenced.Info is { Length: > 0 } info ? info : null, Lines(fenced)),
        CodeBlock code => new Code(null, Lines(code)),
        ThematicBreakBlock => new Rule(),
        HtmlBlock html => new Paragraph([new Fragment(Lines(html))]),
        _ => null,
    };

    private static int Start(ListBlock list) => int.TryParse(list.OrderedStart, out var start) ? start : 1;

    private static Item ItemOf(ListItemBlock item)
    {
        var task = item.FirstOrDefault() is ParagraphBlock { Inline.FirstChild: TaskList box } ? box.Checked : (bool?)null;
        var blocks = Blocks(item);

        // The box is drawn as the item's marker, so its text starts after the space that followed it.
        if (task is not null && blocks.Count > 0 && blocks[0] is Paragraph { Fragments.Count: > 0 } lead)
        {
            List<Fragment> fragments = [.. lead.Fragments];
            fragments[0] = fragments[0] with { Text = fragments[0].Text.TrimStart() };
            blocks[0] = new Paragraph(fragments);
        }

        return new Item(blocks, task);
    }

    private static Table TableOf(MarkdigTable table)
    {
        var columns = table.ColumnDefinitions.Select(column => column.Alignment switch
        {
            TableColumnAlign.Center => Alignment.Center,
            TableColumnAlign.Right => Alignment.End,
            _ => Alignment.Start,
        }).ToList();

        var rows = table.OfType<MarkdigTableRow>()
            .Select(row => new Row(row.IsHeader, [.. row.OfType<TableCell>().Select(CellOf)]))
            .ToList();

        // A row can have fewer cells than the header; the table keeps one column per cell of its widest row.
        while (columns.Count < rows.Select(row => row.Cells.Count).DefaultIfEmpty().Max())
        {
            columns.Add(Alignment.Start);
        }

        return new Table(columns, rows);
    }

    private static IReadOnlyList<Fragment> CellOf(TableCell cell)
    {
        List<Fragment> fragments = [];
        foreach (var paragraph in cell.OfType<LeafBlock>())
        {
            if (fragments.Count > 0)
            {
                fragments.Add(new Fragment(" "));
            }

            fragments.AddRange(Fragments(paragraph.Inline));
        }

        return Merged(fragments);
    }

    private static string Lines(LeafBlock block) =>
        string.Join('\n', block.Lines.Lines.Take(block.Lines.Count).Select(line => line.Slice.ToString()));

    private static List<Fragment> Fragments(ContainerInline? inline)
    {
        List<Fragment> fragments = [];
        if (inline is not null)
        {
            Walk(inline, Look.Plain, null, fragments);
        }

        return Merged(fragments);
    }

    private static void Walk(Inline inline, Look look, string? link, List<Fragment> into)
    {
        switch (inline)
        {
            case LiteralInline literal:
                into.Add(new Fragment(literal.Content.ToString(), look, link));
                break;
            case CodeInline code:
                into.Add(new Fragment(code.Content, look | Look.Code, link));
                break;
            case LineBreakInline lineBreak:
                into.Add(new Fragment(lineBreak.IsHard ? "\n" : " ", look, link));
                break;
            case HtmlEntityInline entity:
                into.Add(new Fragment(entity.Transcoded.ToString(), look, link));
                break;
            case HtmlInline html:
                into.Add(new Fragment(html.Tag, look, link));
                break;
            case AutolinkInline autolink:
                into.Add(new Fragment(autolink.Url, look, autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url));
                break;
            case EmphasisInline emphasis:
                var extra = emphasis.DelimiterChar == '~' ? Look.Strike : emphasis.DelimiterCount >= 2 ? Look.Bold : Look.Italic;
                WalkChildren(emphasis, look | extra, link, into);
                break;
            case LinkInline { IsImage: true } image:
                WalkChildren(image, look, link, into);
                break;
            case LinkInline target:
                WalkChildren(target, look, target.Url ?? link, into);
                break;
            case TaskList:
                break;
            case ContainerInline container:
                WalkChildren(container, look, link, into);
                break;
        }
    }

    private static void WalkChildren(ContainerInline container, Look look, string? link, List<Fragment> into)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            Walk(child, look, link, into);
        }
    }

    // Neighbours with the same look become one fragment, so a run of plain words is one stretch of text.
    private static List<Fragment> Merged(List<Fragment> fragments)
    {
        List<Fragment> merged = [];
        foreach (var fragment in fragments.Where(fragment => fragment.Text.Length > 0))
        {
            if (merged.Count > 0 && merged[^1] is var last && last.Look == fragment.Look && last.Link == fragment.Link)
            {
                merged[^1] = last with { Text = last.Text + fragment.Text };
            }
            else
            {
                merged.Add(fragment);
            }
        }

        return merged;
    }
}
