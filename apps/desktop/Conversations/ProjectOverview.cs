using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Projects;
using Crafter.Desktop.Projects;

namespace Crafter.Desktop.Conversations;

public sealed partial class ProjectOverview : ObservableObject
{
    private const int Latest = 5;

    private readonly ProjectDesk desk;

    private readonly Crew crew;

    private readonly Func<Project, Task> edit;

    public ProjectOverview(ProjectDesk desk, Crew crew, Func<Project, Task> edit)
    {
        this.desk = desk;
        this.crew = crew;
        this.edit = edit;
        Name = string.Empty;
        Letter = string.Empty;
        Describe(desk.Project);
    }

    public ObservableCollection<RecentChat> Recent { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Letter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    public partial Bitmap? Icon { get; set; }

    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    public partial bool HasRecent { get; set; }

    [ObservableProperty]
    public partial string Count { get; set; } = string.Empty;

    public void Describe(Project project)
    {
        Name = project.Name.Text;
        Letter = ProjectIcon.Letter(project);
        Icon = ProjectIcon.Of(project);
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

        var now = DateTimeOffset.Now;
        var latest = found.OrderByDescending(each => each.Past.LastActive).Take(Latest).ToList();
        Recent.Clear();
        foreach (var (agent, past) in latest)
        {
            var open = desk.Chats.FirstOrDefault(chat => chat.SessionId == past.SessionId);
            Recent.Add(new RecentChat(agent, past, open, now) { IsLast = past == latest[^1].Past });
        }

        HasRecent = Recent.Count > 0;
        Count = total > Recent.Count ? $"{Recent.Count} of {total}" : string.Empty;
    }

    [RelayCommand]
    private void NewChat() => desk.NewChat();

    [RelayCommand]
    private Task Edit() => edit(desk.Project);

    [RelayCommand]
    private Task Resume(RecentChat recent) => desk.Resume(recent.Agent, recent.Past);
}
