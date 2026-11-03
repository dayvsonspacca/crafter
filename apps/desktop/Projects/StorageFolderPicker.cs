using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Crafter.Desktop.Projects;

public sealed class StorageFolderPicker(TopLevel window) : IFolderPicker
{
    public async Task<string?> Pick()
    {
        var picked = await window.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Add Project", AllowMultiple = false });

        return picked.Count == 0 ? null : picked[0].TryGetLocalPath();
    }
}
