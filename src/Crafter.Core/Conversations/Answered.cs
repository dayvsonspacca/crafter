using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Conversations;

// An answer as it was given: its text, how it ended, and the steps that make it up in order, the
// words written and the tools used with what they returned, so a reopened chat shows it whole.
public sealed record Answered(string Text, Ending Ending) : Turn
{
    // A tool's output is kept up to this many characters, as the chat shows it.
    public const int LongestOutput = 4000;

    public IReadOnlyList<Reply> Steps { get; init; } = [];

    public bool Equals(Answered? other) =>
        other is not null && Text == other.Text && Ending == other.Ending && Steps.SequenceEqual(other.Steps);

    public override int GetHashCode() => HashCode.Combine(Text, Ending, Steps.Count);
}
