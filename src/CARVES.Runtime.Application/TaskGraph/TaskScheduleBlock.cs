namespace Carves.Runtime.Application.TaskGraph;

public sealed record TaskScheduleBlock(string TaskId, TaskScheduleBlockKind Kind, string Reason);
