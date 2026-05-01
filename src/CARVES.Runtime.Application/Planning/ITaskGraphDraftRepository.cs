using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public interface ITaskGraphDraftRepository
{
    IReadOnlyList<TaskGraphDraftRecord> List();

    TaskGraphDraftRecord? TryGet(string draftId);

    void Save(TaskGraphDraftRecord record);
}
