namespace Crafter.Core.Projects;

public sealed record ProjectName
{
    public ProjectName(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }

    public string Text { get; }

    public static ProjectName Of(string folder) =>
        new(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)) is { Length: > 0 } name ? name : folder);

    public override string ToString() => Text;
}
