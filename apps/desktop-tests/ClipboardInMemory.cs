using Crafter.Desktop.Platform;

namespace Crafter.Desktop.Tests;

internal sealed class ClipboardInMemory : IClipboard
{
    public List<string> Copied { get; } = [];

    public Task Copy(string text)
    {
        Copied.Add(text);

        return Task.CompletedTask;
    }
}
