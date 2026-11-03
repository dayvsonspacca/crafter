namespace Crafter.Presentation.Conversations;

public sealed class YourMessage(string text) : Message
{
    public string Text { get; } = text;
}
