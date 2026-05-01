namespace Carves.Runtime.Application.Workers;

public sealed record WorkerPlan(string TaskId, string Title, bool DryRun, string CurrentCommit, string WorkerAdapterName, string WorktreeRoot);
