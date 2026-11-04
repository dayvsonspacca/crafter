using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Presentation.Preferences;

public abstract partial class PreferenceGroupViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsShown { get; set; }
}
