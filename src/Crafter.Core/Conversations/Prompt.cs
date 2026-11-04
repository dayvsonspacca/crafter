namespace Crafter.Core.Conversations;

public sealed record Prompt
{
    public Prompt(string text, IReadOnlyList<Picture>? pictures = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Pictures = pictures ?? [];
    }

    public string Text { get; }

    public IReadOnlyList<Picture> Pictures { get; }

    public bool Equals(Prompt? other) => other is not null && Text == other.Text && Pictures.SequenceEqual(other.Pictures);

    public override int GetHashCode() => HashCode.Combine(Text, Pictures.Count);
}
