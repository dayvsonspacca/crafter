using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Conversations.Composing;

public sealed partial class MentionViewModel : ObservableObject
{
    public MentionViewModel(string path, IReadOnlyList<int> hits)
    {
        Path = path;
        var trimmed = path.TrimEnd('/');
        var cut = trimmed.LastIndexOf('/') + 1;
        Name = NamePart.Split(trimmed[cut..], hits.Where(hit => hit >= cut && hit < trimmed.Length).Select(hit => hit - cut));
        Folder = cut > 0 ? trimmed[..(cut - 1)] : string.Empty;
    }

    public string Path { get; }

    public IReadOnlyList<NamePart> Name { get; }

    public string Folder { get; }

    public bool HasFolder => Folder.Length > 0;

    public bool IsFolder => Path.EndsWith('/');

    public bool IsFile => !IsFolder;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
