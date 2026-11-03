using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Crafter.Desktop.Platform;

public sealed class DesktopClipboard : Crafter.Presentation.Platform.IClipboard
{
    public Task Copy(string text) =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard }
            ? clipboard.SetTextAsync(text)
            : Task.CompletedTask;
}
