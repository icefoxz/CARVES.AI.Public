using Carves.Runtime.Application.Guard;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private sealed class UnavailableGuardRuntimeTaskRunner : IGuardRuntimeTaskRunner
    {
        public GuardRuntimeExecutionResult Execute(GuardRuntimeTaskInvocation invocation)
        {
            return new GuardRuntimeExecutionResult(
                invocation.TaskId,
                Accepted: false,
                Outcome: "runtime_task_runner_unavailable",
                TaskStatus: "unchanged",
                Summary: "Standalone Guard CLI does not own Runtime task execution. Use diff-only guard check, or use the Runtime compatibility wrapper for task-aware mode.",
                FailureKind: "RuntimeTaskRunnerUnavailable",
                RunId: null,
                ExecutionRunId: null,
                ChangedFiles: Array.Empty<string>(),
                NextAction: "carves-guard check");
        }
    }

    private sealed record GuardCliArguments(
        string? RepoRootOverride,
        IReadOnlyList<string> CommandArguments,
        GuardRuntimeTransportPreference Transport,
        string? Error);
}
