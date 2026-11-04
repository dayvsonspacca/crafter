using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents.Background;

namespace Crafter.Presentation.Conversations.Background;

public sealed partial class JobViewModel(Job job, DateTimeOffset since, Func<string, string?> printed, Action<string> stop) : ObservableObject
{
    private Job job = job;

    private const int Lines = 40;

    public string Id => job.Id;

    public string Description => job.Description;

    public string Kind => job.Kind switch
    {
        JobKind.Monitor => "monitor",
        JobKind.Agent => "agent",
        _ => "shell",
    };

    public bool IsShell => job.Kind == JobKind.Shell;

    public bool IsMonitor => job.Kind == JobKind.Monitor;

    public bool IsAgent => job.Kind == JobKind.Agent;

    public bool HasOutput => job.Prints;

    public DateTimeOffset Since { get; } = since;

    [ObservableProperty]
    public partial string Elapsed { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Output { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStopping { get; set; }

    public void Update(Job changed)
    {
        if (job == changed)
        {
            return;
        }

        job = changed;
        OnPropertyChanged(string.Empty);
        Read();
    }

    public void Tick(DateTimeOffset now)
    {
        var gone = now - Since;
        Elapsed = gone.TotalMinutes < 1 ? $"{(int)gone.TotalSeconds}s" : gone.TotalHours < 1 ? $"{(int)gone.TotalMinutes}m {gone.Seconds}s" : $"{(int)gone.TotalHours}h {gone.Minutes}m";
        if (IsOpen)
        {
            Read();
        }
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            Read();
        }
    }

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen && HasOutput;

    [RelayCommand]
    private void Stop()
    {
        IsStopping = true;
        stop(job.Id);
    }

    private void Read()
    {
        if (!IsOpen || !job.Prints)
        {
            return;
        }

        var lines = (printed(job.Id) ?? string.Empty).TrimEnd().Split('\n');
        Output = string.Join('\n', lines[Math.Max(0, lines.Length - Lines)..]);
        if (Output.Length == 0)
        {
            Output = "No output yet.";
        }
    }
}
