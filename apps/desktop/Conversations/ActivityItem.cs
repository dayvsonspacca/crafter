using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ActivityItem(Acting acting) : ObservableObject
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsFine), nameof(Result))]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFine), nameof(Result))]
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
