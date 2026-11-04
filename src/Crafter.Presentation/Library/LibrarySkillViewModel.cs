using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Library;

namespace Crafter.Presentation.Library;

// One skill in Preferences, with the toggle that gives it to every project.
public sealed partial class LibrarySkillViewModel : ObservableObject
{
    private readonly Action<Skill, bool> makeDefault;

    public LibrarySkillViewModel(Skill skill, bool isDefault, Action<Skill, bool> makeDefault)
    {
        Skill = skill;
        IsDefault = isDefault;
        this.makeDefault = makeDefault;
    }

    public Skill Skill { get; }

    [ObservableProperty]
    public partial bool IsDefault { get; set; }

    partial void OnIsDefaultChanged(bool value) => makeDefault?.Invoke(Skill, value);
}
