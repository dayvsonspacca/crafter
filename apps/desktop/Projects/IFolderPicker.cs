namespace Crafter.Desktop.Projects;

public interface IFolderPicker
{
    Task<string?> Pick();
}
