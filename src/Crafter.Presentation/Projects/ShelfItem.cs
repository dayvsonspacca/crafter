using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Keyboard;

namespace Crafter.Presentation.Projects;

public sealed partial class ShelfItem(Project project, Shortcut? shortcut, IShelfKeeper keeper) : ObservableObject
{
    public Project Project { get; } = project;

    public string Name => Project.Name.Text;

    public string? IconFile { get; } = ProjectIcon.FileOf(project);

    public bool HasIcon => IconFile is not null;

    public string Letter => ProjectIcon.Letter(Project);

    public string Keys { get; } = shortcut?.ToString() ?? string.Empty;

    public bool HasChats => Desk?.Count > 0;

    public bool IsCrafting => Desk?.IsCrafting ?? false;

    public bool HasUnread => Desk?.HasUnread ?? false;

    public string ChatCount => Desk?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial ProjectDesk? Desk { get; set; }

    partial void OnDeskChanged(ProjectDesk? oldValue, ProjectDesk? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= Follow;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += Follow;
        }

        NotifyDerived();
    }

    [RelayCommand]
    private void Open() => keeper.Open(Project);

    [RelayCommand]
    private Task Edit() => keeper.Edit(Project);

    [RelayCommand]
    private void Close() => keeper.Close();

    [RelayCommand]
    private Task CopyPath() => keeper.CopyPath(Project);

    [RelayCommand]
    private void Remove() => keeper.Remove(Project);

    private void Follow(object? sender, PropertyChangedEventArgs changed) => NotifyDerived();

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasChats));
        OnPropertyChanged(nameof(IsCrafting));
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(ChatCount));
    }
}
