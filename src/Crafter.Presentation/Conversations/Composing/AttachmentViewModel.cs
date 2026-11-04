using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Composing;

// Something waiting in the composer: a picture sent as an image, or any other file sent
// as an @ reference the agent reads.
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

    public Picture? Picture { get; }

    public string? File { get; }

    public string Name { get; }

    public bool IsPicture => Picture is not null;

    public bool IsFile => File is not null;

    [RelayCommand]
    private void Remove() => remove(this);
}
