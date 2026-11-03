using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Desktop.Themes;

namespace Crafter.Desktop.Settings;

public sealed partial class ThemeChoice(Theme theme, bool isChosen, Action<ThemeChoice> choose) : ObservableObject
{
    public Theme Theme { get; } = theme;

    [ObservableProperty]
    public partial bool IsChosen { get; set; } = isChosen;

    [RelayCommand]
    private void Choose() => choose(this);
}
