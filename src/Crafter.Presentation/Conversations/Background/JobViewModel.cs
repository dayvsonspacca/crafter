using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents.Background;

namespace Crafter.Presentation.Conversations.Background;

public sealed partial class JobViewModel(Job job, DateTimeOffset since, Action<string> stop) : ObservableObject
{
    private Job job = job;

    private const int Tail = 16 * 1024;

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

    public bool HasOutput => job.Output is not null;

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
        if (!IsOpen || job.Output is not { } file)
        {
            return;
        }

        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = Math.Max(0, stream.Length - Tail);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().TrimEnd().Split('\n');
            var shown = lines[(start > 0 ? 1 : 0)..];
            Output = string.Join('\n', shown[Math.Max(0, shown.Length - Lines)..]);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            Output = string.Empty;
        }

        if (Output.Length == 0)
        {
            Output = "No output yet.";
        }
    }
}
