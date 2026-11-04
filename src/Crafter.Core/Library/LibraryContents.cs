namespace Crafter.Core.Library;

// What the library holds, and the folders it could not read, each with the reason.
public sealed record LibraryContents(IReadOnlyList<Skill> Skills, IReadOnlyList<LibraryProblem> Problems)
{
    public static LibraryContents Empty { get; } = new([], []);
}

public sealed record LibraryProblem(string Place, string Reason);
