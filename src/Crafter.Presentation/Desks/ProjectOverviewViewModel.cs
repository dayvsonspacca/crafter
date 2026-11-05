using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Projects;
using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Desks;

public sealed partial class ProjectOverviewViewModel : ObservableObject
{
    private const int Latest = 5;

    private readonly ProjectDeskViewModel desk;

    private readonly Crew crew;

    private readonly TimeProvider time;

    // Home has no details, skills or axioms of its own to show: only its chats.
    public ProjectOverviewViewModel(ProjectDeskViewModel desk, Crew crew, ProjectDetailsViewModel? details, ProjectBriefViewModel? brief, TimeProvider time)
    {
        this.desk = desk;
        this.crew = crew;
        this.time = time;
        Details = details;
        Brief = brief;
        Name = string.Empty;
        Letter = string.Empty;
        Describe(desk.Project);
    }

    public ObservableCollection<RecentChatViewModel> Recent { get; } = [];

    public ProjectDetailsViewModel? Details { get; }

    public ProjectBriefViewModel? Brief { get; }

    // Home is the one desk with nothing to edit; it shows a house instead of an icon or letter.
    public bool IsHome => Details is null;

    // The page opens on the project's chats.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsChats), nameof(ShowsDetails), nameof(ShowsSkills), nameof(ShowsAxioms))]
    public partial ProjectTab Tab { get; private set; }

    public bool ShowsChats => Tab == ProjectTab.Chats;

    public bool ShowsDetails => Tab == ProjectTab.Details;

    public bool ShowsSkills => Tab == ProjectTab.Skills;

    public bool ShowsAxioms => Tab == ProjectTab.Axioms;

    public bool ShowsLetter => !HasIcon && !IsHome;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Letter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon), nameof(ShowsLetter))]
    public partial string? IconFile { get; set; }

    public bool HasIcon => IconFile is not null;

    [ObservableProperty]
    public partial bool HasRecent { get; set; }

    [ObservableProperty]
    public partial string Count { get; set; } = string.Empty;

    public void Describe(Project project)
    {
        Name = project.Name.Text;
        Letter = ProjectIcon.Letter(project);
        IconFile = ProjectIcon.FileOf(project);
        Details?.Describe(project);
        Brief?.Describe(project);
    }

    public void Show(ProjectTab tab)
    {
        Tab = IsHome ? ProjectTab.Chats : tab;
        switch (Tab)
        {
            case ProjectTab.Skills:
                Brief?.Stock();
                break;
            case ProjectTab.Axioms:
                Brief?.Recall();
                break;
        }
    }

    public async Task Refresh()
    {
        List<(IAgent Agent, PastChat Past)> found = [];
        var total = 0;

        foreach (var agent in crew.Hired)
        {
            if (agent is IKeepsHistory history)
            {
                var recollection = await history.History(desk.Project, Latest);
                found.AddRange(recollection.Latest.Select(past => (agent, past)));
                total += recollection.Total;
            }
        }

        var now = time.GetLocalNow();
        var latest = found.OrderByDescending(each => each.Past.LastActive).Take(Latest).ToList();
        Recent.Clear();
        foreach (var (agent, past) in latest)
        {
            var open = desk.Chats.FirstOrDefault(chat => chat.SessionId == past.SessionId);
            Recent.Add(new RecentChatViewModel(agent, past, open, now) { IsLast = past == latest[^1].Past });
        }

        HasRecent = Recent.Count > 0;
        Count = total > Recent.Count ? $"{Recent.Count} of {total}" : string.Empty;
    }

    [RelayCommand]
    private void NewChat() => desk.NewChat();

    [RelayCommand]
    private void ShowChats() => Show(ProjectTab.Chats);

    [RelayCommand]
    private void ShowDetails() => Show(ProjectTab.Details);

    [RelayCommand]
    private void ShowSkills() => Show(ProjectTab.Skills);

    [RelayCommand]
    private void ShowAxioms() => Show(ProjectTab.Axioms);

    [RelayCommand]
    private Task Resume(RecentChatViewModel recent) => desk.Resume(recent.Agent, recent.Past);
}
