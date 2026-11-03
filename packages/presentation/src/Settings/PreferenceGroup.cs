using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Presentation.Settings;

public abstract partial class PreferenceGroup(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsShown { get; set; }
}
