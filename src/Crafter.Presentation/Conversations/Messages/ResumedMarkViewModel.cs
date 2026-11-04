namespace Crafter.Presentation.Conversations.Messages;

public sealed class ResumedMarkViewModel(string text) : MessageViewModel
{
    public string Text { get; } = text;
}
