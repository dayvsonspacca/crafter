namespace Crafter.Presentation.Conversations.Messages;

public sealed class YourMessageViewModel(string text) : MessageViewModel
{
    public string Text { get; } = text;
}
