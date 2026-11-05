using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Presentation.Conversations.Composing;

// Something waiting in the composer: a picture sent as an image, any other file sent as an @
// reference the agent reads, or one of the project's todos, cited by its number.
public sealed partial class AttachmentViewModel
{
    private readonly Action<AttachmentViewModel> remove;

    public AttachmentViewModel(Picture picture, Action<AttachmentViewModel> remove)
    {
        Picture = picture;
        Name = "Picture";
        this.remove = remove;
    }

    public AttachmentViewModel(string path, string reference, Action<AttachmentViewModel> remove)
    {
        File = reference;
        Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;
        this.remove = remove;
    }

    public AttachmentViewModel(Todo todo, Action<AttachmentViewModel> remove)
    {
        Todo = todo;
        Name = todo.Text;
        this.remove = remove;
    }

    public Picture? Picture { get; }

    public Todo? Todo { get; }

    public string Label => Todo is { } todo ? $"#{todo.Number}" : string.Empty;

    public string? File { get; }

    public string Name { get; }

    public bool IsPicture => Picture is not null;

    public bool IsFile => File is not null;

    public bool IsTodo => Todo is not null;

    [RelayCommand]
    private void Remove() => remove(this);
}
