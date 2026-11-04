using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Messages;

public sealed class YourMessageViewModel(Prompt prompt, string? typed = null, IReadOnlyList<string>? files = null) : MessageViewModel
{
    public Prompt Prompt { get; } = prompt;

    public string Text { get; } = typed ?? prompt.Text;

    public IReadOnlyList<Picture> Pictures => Prompt.Pictures;

    public bool HasPictures => Prompt.Pictures.Count > 0;

    // The names of the files sent along, shown on the message; the agent got them as @ references.
    public IReadOnlyList<string> Files { get; } = files ?? [];

    public bool HasFiles => Files.Count > 0;

    public bool HasText => Text.Length > 0;
}
