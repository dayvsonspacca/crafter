using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Library;
using Crafter.Presentation.Folders;

namespace Crafter.Presentation.Conversations.Composing;

// One suggestion under the composer: a file or folder after @, or one of Crafter's skills after /.
public sealed partial class MentionViewModel : ObservableObject
{
    public MentionViewModel(Skill skill, IReadOnlyList<int> hits)
    {
        Skill = skill;
        Path = skill.Id;
        Name = NamePart.Split(skill.Id, hits);
        Folder = string.Empty;
    }

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

    public Skill? Skill { get; }

    public bool IsSkill => Skill is not null;

    public string Description => Skill?.Description ?? string.Empty;

    public bool IsFolder => !IsSkill && Path.EndsWith('/');

    public bool IsFile => !IsSkill && !IsFolder;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
