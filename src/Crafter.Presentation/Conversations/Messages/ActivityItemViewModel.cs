using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class ActivityItemViewModel(Acting acting) : ObservableObject
{
    private const int Longest = 4000;

    public string Id { get; } = acting.Id;

    public string Tool { get; } = acting.Tool;

    public string Summary { get; } = acting.Summary;

    public bool IsRunning => !IsDone;

    public bool IsFine => IsDone && !IsFailed;

    public bool HasOutput => Output.Length > 0;

    public string Result => this switch
    {
        { IsDone: false } => "running",
        { IsFailed: true } => "failed",
        _ when acting.Stat is { } stat => stat,
        { HasOutput: true } => Lines(Output),
        _ => "done",
    };

    // An edit's line counts, "+12 −3", drawn in the colours of a diff once it went through.
    public bool ShowsChanges => IsFine && Added is not null;

    public bool ShowsResult => !ShowsChanges;

    public string? Added => acting.Stat?.Split(' ')[0] is { } added && added.StartsWith('+') ? added : null;

    public string? Removed => acting.Stat?.Split(' ') is [_, var removed] && removed.StartsWith('−') ? removed : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsFine), nameof(Result), nameof(ShowsChanges), nameof(ShowsResult))]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFine), nameof(Result), nameof(ShowsChanges), nameof(ShowsResult))]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput), nameof(Result))]
    public partial string Output { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public void Finish(Acted acted)
    {
        Output = acted.Output.Length > Longest ? acted.Output[..Longest] + "\n…" : acted.Output;
        IsFailed = acted.Failed;
        IsDone = true;
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = HasOutput && !IsExpanded;

    private static string Lines(string output)
    {
        var count = output.TrimEnd('\n').Split('\n').Length;

        return count == 1 ? "1 line" : $"{count.ToString(CultureInfo.InvariantCulture)} lines";
    }
}
