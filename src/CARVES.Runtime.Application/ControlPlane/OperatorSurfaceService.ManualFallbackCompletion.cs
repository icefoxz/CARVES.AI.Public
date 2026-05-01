using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private TaskNode ReconcileManualFallbackCompletion(TaskNode task, PlannerVerdict verdict, string reason)
    {
        if (verdict != PlannerVerdict.Complete || task.Status != DomainTaskStatus.Completed)
        {
            return task;
        }

        var latestRun = executionRunService.ListRuns(task.TaskId).LastOrDefault();
        var requiresReconciliation = latestRun is null
            ? task.LastWorkerFailureKind != WorkerFailureKind.None
              || task.LastWorkerRetryable
              || task.LastRecoveryAction != WorkerRecoveryAction.None
              || task.RetryNotBefore is not null
            : latestRun.Status is ExecutionRunStatus.Failed
                or ExecutionRunStatus.Stopped
                or ExecutionRunStatus.Abandoned
                or ExecutionRunStatus.Planned
                or ExecutionRunStatus.Running;
        if (!requiresReconciliation)
        {
            return task;
        }

        var hasDelegatedResidue = latestRun is not null
                                  || !string.IsNullOrWhiteSpace(task.LastWorkerRunId)
                                  || !string.IsNullOrWhiteSpace(task.LastWorkerBackend)
                                  || task.LastWorkerFailureKind != WorkerFailureKind.None
                                  || !string.IsNullOrWhiteSpace(task.LastWorkerSummary)
                                  || !string.IsNullOrWhiteSpace(task.LastWorkerDetailRef)
                                  || !string.IsNullOrWhiteSpace(task.LastProviderDetailRef)
                                  || task.LastRecoveryAction != WorkerRecoveryAction.None
                                  || !string.IsNullOrWhiteSpace(task.LastRecoveryReason)
                                  || task.RetryNotBefore is not null;
        if (!hasDelegatedResidue)
        {
            return task;
        }

        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["completion_provenance"] = "manual_fallback",
            ["completion_outcome_status"] = "ManualFallbackCompleted",
            ["completion_recorded_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["completion_reason"] = reason,
            ["fallback_run_packet_role_switch_receipt"] = "manual_fallback_review_boundary_receipt",
            ["fallback_run_packet_context_receipt"] = latestRun?.RunId ?? task.LastWorkerRunId ?? "manual_fallback_no_prior_run_context",
            ["fallback_run_packet_execution_claim"] = $"manual_fallback_execution_claim:{task.TaskId}",
            ["fallback_run_packet_review_bundle"] = $".ai/artifacts/reviews/{task.TaskId}.json#closure_bundle",
        };

        if (latestRun is null)
        {
            metadata.Remove("execution_run_latest_id");
            metadata.Remove("execution_run_latest_status");
            metadata.Remove("execution_run_current_step_index");
            metadata.Remove("execution_run_current_step_title");
            metadata.Remove("execution_run_active_id");
            metadata.Remove("completion_historical_run_id");
            metadata.Remove("completion_historical_run_status");
        }
        else
        {
            metadata["execution_run_latest_id"] = latestRun.RunId;
            metadata["execution_run_latest_status"] = "ManualFallbackCompleted";
            metadata["execution_run_current_step_title"] = "Task completed through manual fallback; delegated execution history preserved separately.";
            metadata["completion_historical_run_id"] = latestRun.RunId;
            metadata["completion_historical_run_status"] = latestRun.Status.ToString();
            metadata.Remove("execution_run_current_step_index");
            metadata.Remove("execution_run_active_id");
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId))
        {
            metadata["completion_historical_worker_run_id"] = task.LastWorkerRunId;
        }
        else
        {
            metadata.Remove("completion_historical_worker_run_id");
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerBackend))
        {
            metadata["completion_historical_worker_backend"] = task.LastWorkerBackend;
        }
        else
        {
            metadata.Remove("completion_historical_worker_backend");
        }

        if (task.LastWorkerFailureKind != WorkerFailureKind.None)
        {
            metadata["completion_historical_worker_failure_kind"] = task.LastWorkerFailureKind.ToString();
        }
        else
        {
            metadata.Remove("completion_historical_worker_failure_kind");
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerSummary))
        {
            metadata["completion_historical_worker_summary"] = task.LastWorkerSummary;
        }
        else
        {
            metadata.Remove("completion_historical_worker_summary");
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerDetailRef))
        {
            metadata["completion_historical_worker_detail_ref"] = task.LastWorkerDetailRef;
        }
        else
        {
            metadata.Remove("completion_historical_worker_detail_ref");
        }

        if (!string.IsNullOrWhiteSpace(task.LastProviderDetailRef))
        {
            metadata["completion_historical_provider_detail_ref"] = task.LastProviderDetailRef;
        }
        else
        {
            metadata.Remove("completion_historical_provider_detail_ref");
        }

        if (task.LastRecoveryAction != WorkerRecoveryAction.None)
        {
            metadata["completion_historical_recovery_action"] = task.LastRecoveryAction.ToString();
        }
        else
        {
            metadata.Remove("completion_historical_recovery_action");
        }

        if (!string.IsNullOrWhiteSpace(task.LastRecoveryReason))
        {
            metadata["completion_historical_recovery_reason"] = task.LastRecoveryReason;
        }
        else
        {
            metadata.Remove("completion_historical_recovery_reason");
        }

        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = metadata,
            LastWorkerRunId = null,
            LastWorkerBackend = null,
            LastWorkerFailureKind = WorkerFailureKind.None,
            LastWorkerRetryable = false,
            LastWorkerSummary = null,
            LastWorkerDetailRef = null,
            LastProviderDetailRef = null,
            LastRecoveryAction = WorkerRecoveryAction.None,
            LastRecoveryReason = null,
            RetryNotBefore = null,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
