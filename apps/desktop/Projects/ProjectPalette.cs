using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Projects;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectPalette : ObservableObject
{
    private readonly Bookshelf bookshelf;

    private readonly Action<Project> open;

    public ProjectPalette(Bookshelf bookshelf, Action<Project> open)
    {
        this.bookshelf = bookshelf;
        this.open = open;
        Query = string.Empty;
    }

    public ObservableCollection<Pick> Picks { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; }

    [ObservableProperty]
    public partial Pick? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool HasPicks { get; set; }

    public void Show()
    {
        Query = string.Empty;
        Restock();
        IsOpen = true;
    }

    public void Hide() => IsOpen = false;

    public void Move(int step)
    {
        if (Picks.Count == 0)
        {
            return;
        }

        var at = Selected is null ? 0 : Picks.IndexOf(Selected);
        Selected = Picks[(at + step + Picks.Count) % Picks.Count];
    }

    public void Choose()
    {
        if (Selected is not { } pick)
        {
            return;
        }

        IsOpen = false;
        open(pick.Project);
    }

    partial void OnQueryChanged(string value) => Restock();

    private void Restock()
    {
        List<Project> shelf = [.. bookshelf.Projects];

        Picks.Clear();
        foreach (var found in bookshelf.Search(Query))
        {
            Picks.Add(new Pick(found, ProjectShortcuts.Slot(shelf.IndexOf(found.Project))));
        }

        Selected = Picks.FirstOrDefault();
        HasPicks = Picks.Count > 0;
    }
}
