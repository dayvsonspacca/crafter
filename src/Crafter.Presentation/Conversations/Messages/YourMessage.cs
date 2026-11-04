namespace Crafter.Presentation.Conversations.Messages;

public sealed class YourMessage(string text) : Message
{
    public string Text { get; } = text;
}
