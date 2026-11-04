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

    // How many library skills the project in front gets, defaults included, and how many axioms it
    // follows; the bar opens both in a window.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BriefLabel))]
    public partial int Skills { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BriefLabel))]
    public partial int Axioms { get; set; }

    public string BriefLabel => (Skills, Axioms) switch
    {
        (0, 0) => "No skills or axioms",
        (_, 0) => Count(Skills, "skill", "skills"),
        (0, _) => Count(Axioms, "axiom", "axioms"),
        _ => $"{Count(Skills, "skill", "skills")} · {Count(Axioms, "axiom", "axioms")}",
    };

    // Home gets the default skills and holds no axioms, so it shows nothing to open.
    [ObservableProperty]
    public partial bool ShowsBrief { get; set; }

    public event EventHandler? BriefWanted;

    [RelayCommand]
    private void ShowBrief() => BriefWanted?.Invoke(this, EventArgs.Empty);

    private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

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
