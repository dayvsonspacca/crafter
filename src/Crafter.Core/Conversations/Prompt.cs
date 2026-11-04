using Crafter.Core.Library;

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

    // The library skills the text calls with /id, which each agent hands over its own way.
    public IReadOnlyList<Skill> Skills { get; init; } = [];

    public bool Equals(Prompt? other) =>
        other is not null && Text == other.Text && Pictures.SequenceEqual(other.Pictures) && Skills.SequenceEqual(other.Skills);

    public override int GetHashCode() => HashCode.Combine(Text, Pictures.Count);
}
