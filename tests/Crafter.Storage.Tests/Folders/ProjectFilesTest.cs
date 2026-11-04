using Crafter.Storage.Folders;

namespace Crafter.Storage.Tests.Folders;

public sealed class ProjectFilesTest
{
    [Fact]
    public void ListsTheFilesAndTheirFoldersRelativeToTheProject()
    {
        using var folders = new TemporaryFolders();
        Write(folders, "README.md");
        Write(folders, "src/Core/Prompt.cs");

        Assert.Equal(["README.md", "src/", "src/Core/", "src/Core/Prompt.cs"], ProjectFiles.Of(folders.Root, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SkipsHiddenAndBuildFolders()
    {
        using var folders = new TemporaryFolders();
        Write(folders, "app.cs");
        Write(folders, ".cache/state.json");
        Write(folders, "node_modules/left-pad/index.js");
        Write(folders, "bin/Debug/app.dll");

        Assert.Equal(["app.cs"], ProjectFiles.Of(folders.Root, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ListsNothingInAFolderThatDoesNotExist()
    {
        Assert.Empty(ProjectFiles.Of("/nonexistent/crafter-folder", TestContext.Current.CancellationToken));
    }

    private static void Write(TemporaryFolders folders, string relative)
    {
        var file = Path.Combine(folders.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, relative);
    }
}
