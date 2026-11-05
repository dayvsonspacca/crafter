namespace Crafter.Core.Projects;

// One thing left to do in a project. Its number names it to the user and to agents alike, so it
// never changes while the todo lives.
public sealed record Todo(int Number, string Text, bool IsDone = false)
{
    public override string ToString() => $"Todo #{Number}: {Text}";
}
