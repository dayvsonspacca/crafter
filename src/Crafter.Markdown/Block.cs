namespace Crafter.Markdown;

// What an answer is made of, as the chat draws it.
public abstract record Block;

public sealed record Paragraph(IReadOnlyList<Fragment> Fragments) : Block;

public sealed record Heading(int Level, IReadOnlyList<Fragment> Fragments) : Block;

public sealed record ItemList(bool Ordered, int Start, IReadOnlyList<Item> Items) : Block;

// Checked is null for a plain item and true or false for a task.
public sealed record Item(IReadOnlyList<Block> Blocks, bool? Checked = null);

public sealed record Quote(IReadOnlyList<Block> Blocks) : Block;

public sealed record Code(string? Language, string Text) : Block;

public sealed record Table(IReadOnlyList<Alignment> Columns, IReadOnlyList<Row> Rows) : Block;

public sealed record Row(bool IsHeader, IReadOnlyList<IReadOnlyList<Fragment>> Cells);

public sealed record Rule : Block;

public enum Alignment
{
    Start,
    Center,
    End,
}
