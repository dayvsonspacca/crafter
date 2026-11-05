using Crafter.Core.Projects;

namespace Crafter.Presentation.Todos;

public interface ITodoKeeper
{
    IReadOnlyList<Todo> TodosOf(Project project);

    Todo? Jot(Project project, string text);

    bool Tick(Project project, int number, bool done);

    bool Scrap(Project project, int number);

    // Hands the todo to the project's chat in front, as a card in its composer.
    void Cite(Project project, Todo todo);
}
