namespace Crafter.Desktop.Folders;

public interface IFolderPicker
{
    Task<string?> Pick(string title, string start);
}
