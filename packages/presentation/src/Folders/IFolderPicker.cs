namespace Crafter.Presentation.Folders;

public interface IFolderPicker
{
    Task<string?> Pick(string title, string start);

    Task<string?> PickImage(string title, string start);
}
