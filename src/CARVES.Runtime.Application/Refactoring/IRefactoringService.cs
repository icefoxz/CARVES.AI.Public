namespace Carves.Runtime.Application.Refactoring;

public interface IRefactoringService
{
    RefactoringBacklogSnapshot LoadBacklog();

    RefactoringBacklogSnapshot DetectAndStore();

    RefactoringTaskMaterializationResult MaterializeSuggestedTasks();
}
