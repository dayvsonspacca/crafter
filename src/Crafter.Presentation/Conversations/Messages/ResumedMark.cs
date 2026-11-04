namespace Crafter.Presentation.Conversations.Messages;

public sealed class ResumedMark(string text) : Message
{
    public string Text { get; } = text;
}
