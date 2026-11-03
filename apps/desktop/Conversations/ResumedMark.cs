namespace Crafter.Desktop.Conversations;

public sealed class ResumedMark(string text) : Message
{
    public string Text { get; } = text;
}
