using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Library;
using Crafter.Core.Preferences;

namespace Crafter.Presentation.Library;

// The skills and agents library: one repository, kept in sync on its own when the app starts,
// when the repository changes and every so often after.
public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(15);

    private readonly PreferenceBook preferences;

    private readonly ILibrary library;

    private readonly TimeProvider time;

    private readonly ITimer timer;

    private readonly SynchronizationContext? ui = SynchronizationContext.Current;

    public LibraryViewModel(PreferenceBook preferences, ILibrary library, TimeProvider time)
    {
        this.preferences = preferences;
        this.library = library;
        this.time = time;
        Draft = preferences.Current.Library ?? string.Empty;
        Status = IsLinked ? "Not synced yet" : Unlinked;
        Show(IsLinked ? library.Read() : LibraryContents.Empty);
        timer = time.CreateTimer(_ => OnUi(() => _ = Sync()), null, Every, Every);

        if (IsLinked)
        {
            _ = Sync();
        }
    }

    private const string Unlinked = "Not linked. Point Crafter at a repository with a skills/ folder.";

    public string? Repository => preferences.Current.Library;

    public bool IsLinked => Repository is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LinkCommand))]
    public partial string Draft { get; set; }

    [ObservableProperty]
    public partial string Status { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    public partial bool IsSyncing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkills), nameof(ShowsEmpty))]
    public partial IReadOnlyList<Skill> Skills { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems))]
    public partial IReadOnlyList<LibraryProblem> Problems { get; private set; } = [];

    public bool HasSkills => Skills.Count > 0;

    public bool HasProblems => Problems.Count > 0;

    public bool ShowsEmpty => IsLinked && !HasSkills && !IsSyncing;

    public void Dispose() => timer.Dispose();

    [RelayCommand(CanExecute = nameof(CanLink))]
    private async Task Link()
    {
        var repository = Draft.Trim();
        preferences.LinkLibrary(repository.Length > 0 ? repository : null);
        OnPropertyChanged(nameof(Repository));
        OnPropertyChanged(nameof(IsLinked));

        if (IsLinked)
        {
            await Sync();
        }
        else
        {
            HasFailed = false;
            Status = Unlinked;
            Show(LibraryContents.Empty);
        }
    }

    private bool CanLink() => Draft.Trim() != (Repository ?? string.Empty);

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task Sync()
    {
        if (Repository is not { } repository || IsSyncing)
        {
            return;
        }

        IsSyncing = true;
        OnPropertyChanged(nameof(ShowsEmpty));
        Status = "Syncing…";
        var outcome = await library.Sync(repository);
        IsSyncing = false;

        // The repository may have changed while git ran; the next sync will catch up.
        if (repository != Repository)
        {
            return;
        }

        switch (outcome)
        {
            case Synced synced:
                HasFailed = false;
                Status = $"Synced at {time.GetLocalNow():HH:mm} · {synced.Commit}";
                Show(library.Read());
                break;
            case SyncFailed failed:
                HasFailed = true;
                Status = $"Could not sync: {failed.Reason}";
                break;
        }

        OnPropertyChanged(nameof(ShowsEmpty));
    }

    private bool CanSync() => !IsSyncing;

    // Writes a skill to the repository, commits and pushes it, then shows the library as it is now.
    public async Task<SyncOutcome> Save(SkillDraft skill)
    {
        if (Repository is not { } repository)
        {
            return new SyncFailed("no library repository is linked; link one in Preferences → Library");
        }

        var outcome = await library.Save(skill, repository);
        if (outcome is Synced synced)
        {
            HasFailed = false;
            Status = $"Synced at {time.GetLocalNow():HH:mm} · {synced.Commit}";
            Show(library.Read());
        }

        return outcome;
    }

    private void Show(LibraryContents contents)
    {
        Skills = contents.Skills;
        Problems = contents.Problems;
    }

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
