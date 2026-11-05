using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Conversations.Messages;

public sealed class YourMessageViewModel(Prompt prompt, string? typed = null, IReadOnlyList<string>? files = null, IReadOnlyList<Todo>? todos = null) : MessageViewModel
{
    public Prompt Prompt { get; } = prompt;

    public string Text { get; } = typed ?? prompt.Text;

    public IReadOnlyList<Picture> Pictures => Prompt.Pictures;

    public bool HasPictures => Prompt.Pictures.Count > 0;

    // The names of the files sent along, shown on the message; the agent got them as @ references.
    public IReadOnlyList<string> Files { get; } = files ?? [];

    public bool HasFiles => Files.Count > 0;

    // The todos cited, shown as cards; the agent got each as a line with its number.
    public IReadOnlyList<Todo> Todos { get; } = todos ?? [];

    public bool HasTodos => Todos.Count > 0;

    public bool HasText => Text.Length > 0;
}
