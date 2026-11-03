using Crafter.Desktop.Folders;

namespace Crafter.Desktop.Tests;

internal sealed class ImagePicker(string? chosen) : IFolderPicker
{
    public Task<string?> Pick(string title, string start) => Task.FromResult<string?>(null);

    public Task<string?> PickImage(string title, string start) => Task.FromResult(chosen);
}
