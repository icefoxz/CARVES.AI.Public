using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.TaskGraph;

public sealed record TaskConflict(TaskConflictKind Kind, TaskNode BlockingTask, string Reason);
