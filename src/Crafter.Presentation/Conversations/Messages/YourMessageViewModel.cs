using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Messages;

public sealed class YourMessageViewModel(Prompt prompt, string? typed = null) : MessageViewModel
{
    public Prompt Prompt { get; } = prompt;

    public string Text { get; } = typed ?? prompt.Text;

    public IReadOnlyList<Picture> Pictures => Prompt.Pictures;

    public bool HasPictures => Prompt.Pictures.Count > 0;

    public bool HasText => Text.Length > 0;
}
