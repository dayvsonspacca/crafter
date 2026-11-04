using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Presentation.Conversations.Background;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations;

public sealed partial class ConversationViewModel : ObservableObject
{
    // A chat opens on its latest few exchanges and brings earlier ones in this many at a time:
    // every answer on screen costs to draw and more to take down when the chat closes.
    public const int Window = 5;

    // The most exchanges a chat keeps on screen as it goes on; past this, sending moves the oldest
    // back among the earlier ones, so a long session costs no more to close than a short one.
    public const int Kept = 10;

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly Chat chat;

    private readonly IAgent agent;

    private readonly PreferenceBook preferences;

    private readonly IChatKeeper keeper;

    private readonly IClipboard clipboard;

    private readonly TimeProvider time;

    private readonly SynchronizationContext? ui = SynchronizationContext.Current;

    private readonly List<MessageViewModel> earlier = [];

    private readonly Attachments attachments = new();

    private readonly IProjectFiles files;

    private readonly Func<IReadOnlyList<Skill>> skills;

    private readonly ITimer clock;

    private DateTimeOffset started;

    // A second Escape within this clears the draft, as in Claude Code.
    public static readonly TimeSpan ClearWindow = TimeSpan.FromSeconds(1);

    private ITimer? disarm;

    private YourMessageViewModel? lastAsked;

    private int turning;

    public ConversationViewModel(Chat chat, IAgent agent, PreferenceBook preferences, string projectName, IChatKeeper keeper, IClipboard clipboard, IProjectFiles files, TimeProvider time, string? resumed = null, Func<IReadOnlyList<Skill>>? skills = null)
    {
        this.skills = skills ?? (() => []);
        this.chat = chat;
        this.agent = agent;
        this.preferences = preferences;
        this.keeper = keeper;
        this.clipboard = clipboard;
        this.files = files;
        this.time = time;
        Mentions = new MentionsViewModel(files, this.skills);
        AgentName = agent.Name;
        ProjectName = projectName;
        Draft = string.Empty;
        Elapsed = string.Empty;
        clock = time.CreateTimer(_ => OnUi(Tick), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Restore(resumed);
        Follow(chat.Jobs);
        chat.JobsChanged += (_, jobs) => OnUi(() => Follow(jobs));
        chat.Woke += (_, replies) => OnUi(() => _ = Wake(replies));
    }

    public event EventHandler? Concluded;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public ObservableCollection<AttachmentViewModel> Attached { get; } = [];

    public MentionsViewModel Mentions { get; }

    public bool HasAttached => Attached.Count > 0;

    public ObservableCollection<JobViewModel> Jobs { get; } = [];

    public bool HasJobs => Jobs.Count > 0;

    public string JobsLabel => Jobs.Count switch
    {
        0 => "No jobs",
        1 => "1 job",
        var count => $"{count} jobs",
    };

    // The background jobs window, opened from the status bar or with Ctrl+J.
    [ObservableProperty]
    public partial bool IsJobsOpen { get; set; }

    [ObservableProperty]
    public partial string ProjectName { get; set; }

    public string AgentName { get; }

    public string Title => chat.Title.Text;

    public bool IsCrafting => State == ChatState.Crafting;

    public bool IsUnread => State == ChatState.Unread;

    public bool IsIdle => State == ChatState.Idle;

    public bool HasEarlier => earlier.Count > 0;

    public string EarlierLabel => $"Show {earlier.Count} earlier messages";

    public bool HasModel => Model is not null;

    [ObservableProperty]
    public partial string Draft { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModel))]
    public partial string? Model { get; set; }

    [ObservableProperty]
    public partial string Elapsed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsClock))]
    public partial bool IsWaiting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsClock))]
    public partial bool IsAsking { get; set; }

    public bool ShowsClock => IsAsking && !IsWaiting;

    [ObservableProperty]
    public partial bool HasMessages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrafting), nameof(IsUnread), nameof(IsIdle))]
    public partial ChatState State { get; set; }

    [ObservableProperty]
    public partial bool IsInView { get; set; }

    public string? SessionId => chat.SessionId;

    private Configuration Preferred => preferences.Current.ConfigurationOf(agent.Id);

    public ChatRecord Record() => chat.Record();

    public bool Runs(IAgentSession session) => chat.Runs(session);

    // An axiom the agent proposes mid-answer shows in that answer; with no answer under way there is
    // no one to ask, so it is declined.
    public Task<bool> ProposeAxiom(string project, string axiom) =>
        Messages.OfType<CrafterMessageViewModel>().LastOrDefault(answer => answer.IsCrafting) is { } answering
            ? answering.Propose(project, axiom)
            : Task.FromResult(false);

    public void Leave()
    {
        if (CraftCommand.IsRunning)
        {
            CraftCommand.Cancel();
        }

        chat.Dismiss();
    }

    public async Task LeaveAsync()
    {
        Leave();

        if (CraftCommand.ExecutionTask is { } running)
        {
            await running;
        }
    }

    public string Paste(string text) => attachments.Paste(text);

    public void Attach(Picture picture)
    {
        attachments.Attach(picture);
        Attached.Add(new AttachmentViewModel(picture, Detach));
        OnPropertyChanged(nameof(HasAttached));
    }

    // Any file that is not a picture: the agent reads it from its @ reference.
    public void AttachFile(string path)
    {
        var reference = Mention(path).TrimEnd();
        if (Attached.Any(each => each.File == reference))
        {
            return;
        }

        attachments.Attach(reference);
        Attached.Add(new AttachmentViewModel(path, reference, Detach));
        OnPropertyChanged(nameof(HasAttached));
    }

    // Escape stops the agent and hands the prompt back to edit; with nothing running, a second
    // Escape soon after the first clears the draft and its attachments.
    public void Escape()
    {
        if (IsAsking)
        {
            Interrupt();

            return;
        }

        if (Draft.Length == 0 && Attached.Count == 0)
        {
            return;
        }

        if (IsClearArmed)
        {
            ClearDraft();

            return;
        }

        IsClearArmed = true;
        disarm?.Dispose();
        disarm = time.CreateTimer(_ => OnUi(() => IsClearArmed = false), null, ClearWindow, Timeout.InfiniteTimeSpan);
    }

    [ObservableProperty]
    public partial bool IsClearArmed { get; private set; }

    // Why the draft was not sent: it opened with a command that is not one of Crafter's skills.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRefused))]
    public partial string? Refusal { get; private set; }

    public bool IsRefused => Refusal is not null;

    public void DetachLast()
    {
        if (Attached.Count > 0)
        {
            Detach(Attached[^1]);
        }
    }

    public string Mention(string path)
    {
        var relative = Path.GetRelativePath(files.Folder, path);
        var inside = !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        var shown = (inside ? relative : path).Replace(Path.DirectorySeparatorChar, '/');

        return $"@{(shown.Any(char.IsWhiteSpace) ? $"\"{shown}\"" : shown)} ";
    }

    partial void OnIsInViewChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        if (State == ChatState.Unread)
        {
            State = ChatState.Idle;
        }
    }

    [RelayCommand]
    private void ToggleJobs() => IsJobsOpen = !IsJobsOpen;

    [RelayCommand]
    private void CloseJobs() => IsJobsOpen = false;

    [RelayCommand]
    private void Select() => keeper.Select(this);

    [RelayCommand]
    private void Close() => keeper.Close(this);

    [RelayCommand]
    private void ShowEarlier()
    {
        var shown = Turns(earlier, Window);
        earlier.RemoveRange(earlier.Count - shown.Count, shown.Count);

        for (var at = shown.Count - 1; at >= 0; at--)
        {
            Messages.Insert(0, shown[at]);
        }

        OnPropertyChanged(nameof(HasEarlier));
        OnPropertyChanged(nameof(EarlierLabel));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Craft(YourMessageViewModel? again, CancellationToken stop)
    {
        if (IsAsking || (again is null ? attachments.Compose(Draft) : again.Prompt) is not { } composed)
        {
            return;
        }

        var given = skills();
        var known = given.Select(skill => skill.Id).ToList();
        if (again is null && SkillCalls.Stranger(Draft, known) is { } stranger)
        {
            Refusal = $"/{stranger} is not a Crafter skill";

            return;
        }

        var called = SkillCalls.In(composed.Text, known);
        var prompt = again is null ? composed with { Skills = [.. given.Where(skill => called.Contains(skill.Id))] } : composed;

        var asked = again is null
            ? new YourMessageViewModel(prompt, Draft.Trim(), [.. Attached.Where(each => each.IsFile).Select(each => each.Name)])
            : new YourMessageViewModel(prompt, again.Text, again.Files);
        var answer = Answer(woken: false);

        if (again is null)
        {
            Draft = string.Empty;
            attachments.Clear();
            Attached.Clear();
            OnPropertyChanged(nameof(HasAttached));
            Mentions.Close();
        }

        Messages.Add(asked);
        Trim();
        lastAsked = asked;
        IsAsking = true;
        started = time.GetUtcNow();

        var replies = chat.Ask(prompt, Preferred, stop);
        OnPropertyChanged(nameof(Title));

        await Hear(answer, replies);
        IsAsking = false;
        Clock();
    }

    private async Task Wake(IAsyncEnumerable<Reply> replies) => await Hear(Answer(woken: true), replies);

    private CrafterMessageViewModel Answer(bool woken)
    {
        var answer = new CrafterMessageViewModel(clipboard, Retry, AgentName, chat.Decide) { IsWoken = woken };
        answer.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(CrafterMessageViewModel.IsWaiting))
            {
                IsWaiting = Messages.OfType<CrafterMessageViewModel>().Any(each => each.IsWaiting);
            }
        };

        return answer;
    }

    private async Task Hear(CrafterMessageViewModel answer, IAsyncEnumerable<Reply> replies)
    {
        Messages.Add(answer);
        Mark();
        HasMessages = true;
        turning++;
        State = ChatState.Crafting;
        Clock();
        Tick();

        await foreach (var reply in replies)
        {
            if (reply is Began began)
            {
                Model = began.Model;
            }

            answer.Hear(reply);
        }

        turning--;
        IsWaiting = Messages.OfType<CrafterMessageViewModel>().Any(each => each.IsWaiting);
        if (turning == 0)
        {
            State = IsInView ? ChatState.Idle : ChatState.Unread;
        }

        Concluded?.Invoke(this, EventArgs.Empty);
    }

    private void Follow(IReadOnlyList<Job> jobs)
    {
        foreach (var gone in Jobs.Where(shown => jobs.All(job => job.Id != shown.Id)).ToList())
        {
            Jobs.Remove(gone);
        }

        foreach (var job in jobs)
        {
            if (Jobs.FirstOrDefault(shown => shown.Id == job.Id) is { } shown)
            {
                shown.Update(job);
            }
            else
            {
                Jobs.Add(new JobViewModel(job, time.GetUtcNow(), chat.Stop));
            }
        }

        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(JobsLabel));
        Clock();
        Tick();
    }

    private void Clock()
    {
        if (IsAsking || Jobs.Count > 0)
        {
            clock.Change(Second, Second);
        }
        else
        {
            clock.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    // Typing after the first Escape means the draft is wanted after all.
    partial void OnDraftChanged(string value)
    {
        IsClearArmed = false;
        Refusal = null;
    }

    private void Interrupt()
    {
        CraftCancelCommand.Execute(null);

        // What was typed meanwhile stays; otherwise the prompt comes back to be edited.
        if (lastAsked is not { } asked || Draft.Length > 0 || Attached.Count > 0)
        {
            return;
        }

        Draft = asked.Text;
        foreach (var picture in asked.Pictures)
        {
            Attach(picture);
        }
    }

    private void ClearDraft()
    {
        disarm?.Dispose();
        IsClearArmed = false;
        Draft = string.Empty;
        while (Attached.Count > 0)
        {
            DetachLast();
        }
    }

    private void Retry(CrafterMessageViewModel answer)
    {
        var place = Messages.IndexOf(answer);
        if (IsAsking || place < 1 || Messages[place - 1] is not YourMessageViewModel asked)
        {
            return;
        }

        CraftCommand.Execute(asked);
    }

    private void Detach(AttachmentViewModel attachment)
    {
        if (attachment.Picture is { } picture)
        {
            attachments.Detach(picture);
        }
        else if (attachment.File is { } file)
        {
            attachments.Detach(file);
        }

        Attached.Remove(attachment);
        OnPropertyChanged(nameof(HasAttached));
    }

    private void Tick()
    {
        var now = time.GetUtcNow();
        foreach (var job in Jobs)
        {
            job.Tick(now);
        }

        if (!IsAsking)
        {
            return;
        }

        var gone = now - started;
        Elapsed = gone.TotalMinutes < 1 ? $"crafting · {(int)gone.TotalSeconds}s" : $"crafting · {(int)gone.TotalMinutes}m {gone.Seconds}s";
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

    private void Restore(string? resumed)
    {
        List<MessageViewModel> told = [];
        foreach (var turn in chat.Turns)
        {
            switch (turn)
            {
                case Asked asked:
                    told.Add(new YourMessageViewModel(asked.Prompt));
                    break;
                case Answered answered:
                    told.Add(new CrafterMessageViewModel(answered.Text, answered.Steps, answered.Ending, clipboard, Retry));
                    break;
            }
        }

        var shown = Turns(told, Window);
        earlier.AddRange(told[..^shown.Count]);
        foreach (var message in shown)
        {
            Messages.Add(message);
        }

        if (resumed is not null && told.Count > 0)
        {
            Messages.Add(new ResumedMarkViewModel(resumed));
        }

        Mark();
        HasMessages = Messages.Count > 0;
    }

    private void Trim()
    {
        var asks = Messages.Count(message => message is YourMessageViewModel);
        while (asks > Kept)
        {
            var first = IndexOfAsk(0);
            var next = IndexOfAsk(first + 1);
            for (var at = 0; at < next; at++)
            {
                earlier.Add(Messages[0]);
                Messages.RemoveAt(0);
            }

            asks--;
        }

        OnPropertyChanged(nameof(HasEarlier));
        OnPropertyChanged(nameof(EarlierLabel));
    }

    private int IndexOfAsk(int from)
    {
        for (var at = from; at < Messages.Count; at++)
        {
            if (Messages[at] is YourMessageViewModel)
            {
                return at;
            }
        }

        return Messages.Count;
    }

    private void Mark()
    {
        for (var at = 0; at < Messages.Count; at++)
        {
            Messages[at].IsLast = at == Messages.Count - 1 || Messages[at + 1] is ResumedMarkViewModel && at + 1 == Messages.Count - 1;
        }
    }

    private static List<MessageViewModel> Turns(List<MessageViewModel> messages, int count)
    {
        var asks = 0;
        var from = messages.Count;

        while (from > 0 && asks < count)
        {
            from--;
            if (messages[from] is YourMessageViewModel)
            {
                asks++;
            }
        }

        return messages[from..];
    }
}
