namespace Crafter.Presentation.Projects;

public sealed record FolderLabel(string Folder)
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string Text => AtHome ? "~" + Folder[Home.Length..] : Folder;

    private bool AtHome =>
        Home.Length > 0
        && (Folder == Home || Folder.StartsWith(Home + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    public static string Expand(string typed) =>
        typed == "~" ? Home : typed.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(Home, typed[2..]) : typed;

    public override string ToString() => Text;
}
