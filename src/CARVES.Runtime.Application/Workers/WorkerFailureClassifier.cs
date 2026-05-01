using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

internal sealed class WorkerFailureClassifier
{
    private readonly WorkerFailureInterpreter interpreter = new();

    public WorkerExecutionResult Normalize(WorkerExecutionResult result, WorkerValidationOutcome validationOutcome)
    {
        if (result.Status == WorkerExecutionStatus.Succeeded && !validationOutcome.Passed)
        {
            return result with
            {
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.TaskLogicFailed,
                FailureReason = "Validation failed after worker execution.",
                Summary = "Worker execution completed but validation failed.",
                Retryable = false,
            };
        }

        if (result.Succeeded || result.Status == WorkerExecutionStatus.Skipped)
        {
            return result;
        }

        if (result.Status == WorkerExecutionStatus.ApprovalWait)
        {
            return result with
            {
                FailureKind = WorkerFailureKind.ApprovalRequired,
                Retryable = false,
            };
        }

        var interpretation = interpreter.Interpret(result);
        return result with
        {
            FailureKind = interpretation.FailureKind,
            FailureLayer = result.FailureLayer == WorkerFailureLayer.None
                ? MapFailureLayer(interpretation.FailureKind)
                : result.FailureLayer,
            Retryable = ResolveRetryable(result, interpretation),
            Summary = string.IsNullOrWhiteSpace(result.Summary) ? interpretation.Summary : result.Summary,
            FailureReason = string.IsNullOrWhiteSpace(result.FailureReason) ? interpretation.Summary : result.FailureReason,
        };
    }

    public WorkerExecutionResult FromException(WorkerExecutionRequest request, IWorkerAdapter adapter, Exception exception)
    {
        var preview = request.Input.Length > 160 ? request.Input[..160] : request.Input;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
        var interpretation = interpreter.InterpretException(request, adapter, exception);
        return WorkerExecutionResult.Blocked(
            request.TaskId,
            adapter.BackendId,
            adapter.ProviderId,
            adapter.AdapterId,
            request.Profile,
            interpretation.FailureKind,
            interpretation.Summary,
            preview,
            hash) with
        {
            Retryable = interpretation.Retryable,
        };
    }

    public TimeSpan ResolveBackoff(TaskNode task, WorkerExecutionResult result)
    {
        if (!result.Retryable)
        {
            return TimeSpan.Zero;
        }

        var retryStep = Math.Max(1, task.RetryCount + 1);
        return result.FailureKind switch
        {
            WorkerFailureKind.TransientInfra => TimeSpan.FromSeconds(Math.Min(30, retryStep * 5)),
            WorkerFailureKind.Timeout => TimeSpan.FromSeconds(Math.Min(60, retryStep * 10)),
            WorkerFailureKind.InvalidOutput => TimeSpan.FromSeconds(Math.Min(15, retryStep * 3)),
            _ => TimeSpan.FromSeconds(0),
        };
    }

    private static bool IsRetryable(WorkerFailureKind failureKind)
    {
        return WorkerFailureInterpreter.IsRetryable(failureKind);
    }

    private static bool ResolveRetryable(WorkerExecutionResult result, WorkerFailureInterpretation interpretation)
    {
        if (result.FailureKind == WorkerFailureKind.InvalidOutput
            && result.FailureLayer == WorkerFailureLayer.Protocol)
        {
            return result.Retryable;
        }

        return interpretation.Retryable;
    }

    private static WorkerFailureLayer MapFailureLayer(WorkerFailureKind failureKind)
    {
        return failureKind switch
        {
            WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.PolicyDenied or WorkerFailureKind.ApprovalRequired => WorkerFailureLayer.Environment,
            WorkerFailureKind.LaunchFailure or WorkerFailureKind.AttachFailure or WorkerFailureKind.WrapperFailure or WorkerFailureKind.ArtifactFailure => WorkerFailureLayer.Environment,
            WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout => WorkerFailureLayer.Transport,
            WorkerFailureKind.InvalidOutput => WorkerFailureLayer.Protocol,
            WorkerFailureKind.ContractFailure => WorkerFailureLayer.Protocol,
            WorkerFailureKind.TaskLogicFailed or WorkerFailureKind.BuildFailure or WorkerFailureKind.TestFailure or WorkerFailureKind.PatchFailure => WorkerFailureLayer.WorkerSemantic,
            _ => WorkerFailureLayer.None,
        };
    }
}
