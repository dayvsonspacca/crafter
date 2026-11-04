using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Projects;
using Crafter.Presentation.Conversations;

namespace Crafter.Presentation.Status;

// The bar along the bottom of the window. It shows the branch of the project in front and
// reads it again every couple of seconds, since an agent can switch branches mid-chat.
public sealed partial class StatusBarViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    private readonly IBranches branches;

    private readonly ITimer timer;

    private readonly SynchronizationContext? ui = SynchronizationContext.Current;

    private Project? project;

    public StatusBarViewModel(IBranches branches, TimeProvider time)
    {
        this.branches = branches;
        timer = time.CreateTimer(_ => OnUi(Refresh), null, Every, Every);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBranch))]
    public partial string? Branch { get; private set; }

    public bool HasBranch => Branch is not null;

    // How many library skills the project in front gets, defaults included; the bar opens them
    // in a window.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SkillsLabel))]
    public partial int Skills { get; set; }

    public string SkillsLabel => Skills switch
    {
        0 => "No skills",
        1 => "1 skill",
        _ => $"{Skills} skills",
    };

    // Home gets the defaults and nothing to choose, so it shows no skills to open.
    [ObservableProperty]
    public partial bool ChoosesSkills { get; set; }

    public event EventHandler? SkillsWanted;

    [RelayCommand]
    private void ShowSkills() => SkillsWanted?.Invoke(this, EventArgs.Empty);

    // The chat in front, whose background jobs the bar counts and opens.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChat))]
    public partial ConversationViewModel? Chat { get; set; }

    public bool HasChat => Chat is not null;

    public void Watch(Project? watched)
    {
        project = watched;
        Refresh();
    }

    public void Refresh() => Branch = project is null ? null : branches.Of(project);

    public void Dispose() => timer.Dispose();

    private void OnUi(Action action)
    {
        if (ui is null)
        {
            action();
        }
        else
        {
            ui.Post(_ => action(), null);
        }
    }
}
