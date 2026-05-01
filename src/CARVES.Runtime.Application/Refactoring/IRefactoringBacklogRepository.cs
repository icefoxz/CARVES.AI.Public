namespace Carves.Runtime.Application.Refactoring;

public interface IRefactoringBacklogRepository
{
    RefactoringBacklogSnapshot Load();

    void Save(RefactoringBacklogSnapshot snapshot);
}
