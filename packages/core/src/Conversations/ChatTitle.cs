namespace Crafter.Core.Conversations;

public sealed record ChatTitle
{
    private const int Longest = 40;

    private ChatTitle(string text) => Text = text;

    public static ChatTitle Untitled { get; } = new("New chat");

    public string Text { get; }

    public static ChatTitle Of(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new ChatTitle(text.Trim());
    }

    public static ChatTitle From(Prompt first)
    {
        var line = first.Text.Trim().Split('\n')[0].Trim();

        return new ChatTitle(line.Length <= Longest ? line : line[..(Longest - 1)].TrimEnd() + "…");
    }

    public override string ToString() => Text;
}
