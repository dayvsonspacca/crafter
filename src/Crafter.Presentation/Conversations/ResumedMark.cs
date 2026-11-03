namespace Crafter.Presentation.Conversations;

public sealed class ResumedMark(string text) : Message
{
    public string Text { get; } = text;
}
