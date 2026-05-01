using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed partial class DevLoopService
{
    private void HandleFailure(RuntimeSessionState session, string? taskId, Exception exception)
    {
        var failure = runtimeFailurePolicy.ClassifyException(session, taskId, exception);
        ApplyTaskFailure(taskId, failure);
        ApplySessionFailure(session, failure);

        try
        {
            artifactRepository.SaveRuntimeFailureArtifact(failure);
        }
        catch (Exception persistenceException) when (persistenceException is IOException or UnauthorizedAccessException)
        {
            session.Fail($"{failure.Reason} Failure artifact persistence also failed: {persistenceException.Message}");
        }

        failureReportService.EmitRuntimeFailure(TryGetTask(taskId), failure, session);
    }

    private void HandleWorkerFailure(RuntimeSessionState session, WorkerRequest? request, TaskNode completedTask, WorkerExecutionResult result)
    {
        var recovery = recoveryPolicyEngine.Evaluate(completedTask, result);
        session.RecordRecoveryDecision(completedTask.TaskId, recovery);
        incidentTimelineService.Append(new RuntimeIncidentRecord
        {
            IncidentType = RuntimeIncidentType.RecoverySelected,
            RepoId = "local-repo",
            TaskId = completedTask.TaskId,
            RunId = result.RunId,
            BackendId = result.BackendId,
            ProviderId = result.ProviderId,
            FailureKind = result.FailureKind,
            RecoveryAction = recovery.Action,
            ActorKind = RuntimeIncidentActorKind.Policy,
            ActorIdentity = nameof(RecoveryPolicyEngine),
            ReasonCode = recovery.ReasonCode,
            Summary = recovery.Reason,
            ConsequenceSummary = DescribeRecoveryConsequence(recovery),
            ReferenceId = result.RunId,
        });

        if (recovery.Action == WorkerRecoveryAction.RebuildWorktree && request is not null)
        {
            var record = worktreeRuntimeService.QuarantineAndRequestRebuild(completedTask.TaskId, request.Session.WorktreeRoot, recovery.Reason);
            if (record is not null)
            {
                incidentTimelineService.Append(new RuntimeIncidentRecord
                {
                    IncidentType = RuntimeIncidentType.WorktreeQuarantined,
                    RepoId = "local-repo",
                    TaskId = completedTask.TaskId,
                    RunId = result.RunId,
                    BackendId = result.BackendId,
                    ProviderId = result.ProviderId,
                    FailureKind = result.FailureKind,
                    RecoveryAction = WorkerRecoveryAction.RebuildWorktree,
                    ActorKind = RuntimeIncidentActorKind.System,
                    ActorIdentity = nameof(WorktreeRuntimeService),
                    ReasonCode = "worktree_quarantined",
                    Summary = recovery.Reason,
                    ConsequenceSummary = $"Worktree '{record.WorktreePath}' was quarantined and a rebuild was requested.",
                    ReferenceId = record.RecordId,
                });
            }
        }

        var failure = runtimeFailurePolicy.ClassifyWorkerFailure(session, completedTask.TaskId, result, recovery);
        ApplyTaskFailure(completedTask.TaskId, failure, recovery, result);
        ApplySessionFailure(session, failure);

        try
        {
            artifactRepository.SaveRuntimeFailureArtifact(failure);
        }
        catch (Exception persistenceException) when (persistenceException is IOException or UnauthorizedAccessException)
        {
            session.Fail($"{failure.Reason} Failure artifact persistence also failed: {persistenceException.Message}");
        }

        failureReportService.EmitRuntimeFailure(completedTask, failure, session);
    }

    private void ApplyTaskFailure(string? taskId, RuntimeFailureRecord failure, WorkerRecoveryDecision? recovery = null, WorkerExecutionResult? workerResult = null)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        var graph = taskGraphService.Load();
        if (!graph.Tasks.TryGetValue(taskId, out var task) || task.Status == DomainTaskStatus.Completed || task.Status == DomainTaskStatus.Failed)
        {
            return;
        }

        var review = failure.Action switch
        {
            RuntimeFailureAction.RetryTask or RuntimeFailureAction.RebuildWorktree or RuntimeFailureAction.SwitchProvider => new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = $"Runtime recovery selected: {failure.Action}. {failure.Reason}",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = [$"Review runtime failure artifact {failure.FailureId} before the next attempt."],
            },
            RuntimeFailureAction.BlockTask => new PlannerReview
            {
                Verdict = PlannerVerdict.Blocked,
                Reason = $"Runtime blocked the task after recovery evaluation: {failure.Reason}",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = [$"Review runtime failure artifact {failure.FailureId} before continuing."],
            },
            _ => new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = $"Runtime failure requires operator attention: {failure.FailureType}. {failure.Reason}",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = [$"Review runtime failure artifact {failure.FailureId} before continuing."],
            },
        };

        task.SetPlannerReview(review);
        if (recovery is not null)
        {
            task.RecordRecovery(recovery);
        }

        var classification = workerResult is null ? null : WorkerFailureSemantics.Classify(workerResult);
        if (classification is not null)
        {
            task = CloneTaskWithMetadata(task, "execution_failure_lane", classification.Lane.ToString().ToLowerInvariant());
            task = CloneTaskWithMetadata(task, "execution_failure_reason_code", classification.ReasonCode);
            task = CloneTaskWithMetadata(task, "execution_replan_allowed", classification.ReplanAllowed ? "true" : "false");
            task = CloneTaskWithMetadata(task, "execution_failure_next_action", classification.NextAction);
            task = CloneTaskWithMetadata(task, "execution_substrate_failure", classification.Lane == WorkerFailureLane.Substrate ? "true" : "false");
            task = CloneTaskWithMetadata(task, "execution_substrate_category", classification.SubstrateCategory ?? string.Empty);
            task = CloneTaskWithMetadata(task, "execution_substrate_next_action", classification.Lane == WorkerFailureLane.Substrate ? failure.Action.ToString() : string.Empty);
        }

        switch (failure.Action)
        {
            case RuntimeFailureAction.RetryTask:
                task.IncrementRetryCount();
                task.SetStatus(DomainTaskStatus.Pending);
                task.RetryNotBefore = recovery?.RetryNotBefore;
                break;
            case RuntimeFailureAction.RebuildWorktree:
                task.IncrementRetryCount();
                task.SetStatus(DomainTaskStatus.Pending);
                task.RetryNotBefore = recovery?.RetryNotBefore;
                break;
            case RuntimeFailureAction.SwitchProvider:
                task.IncrementRetryCount();
                task.SetStatus(DomainTaskStatus.Pending);
                task.RetryNotBefore = recovery?.RetryNotBefore;
                if (!string.IsNullOrWhiteSpace(recovery?.AlternateBackendId))
                {
                    task = CloneTaskWithMetadata(task, "worker_backend", recovery.AlternateBackendId!);
                }
                break;
            case RuntimeFailureAction.AbortTask:
                task.SetStatus(DomainTaskStatus.Failed);
                break;
            case RuntimeFailureAction.BlockTask:
                task.SetStatus(DomainTaskStatus.Blocked);
                break;
            case RuntimeFailureAction.PauseSession:
            case RuntimeFailureAction.EscalateToOperator:
                task.SetStatus(DomainTaskStatus.Pending);
                break;
        }

        taskGraphService.ReplaceTask(task);
    }

    private static void ApplySessionFailure(RuntimeSessionState session, RuntimeFailureRecord failure)
    {
        switch (failure.Action)
        {
            case RuntimeFailureAction.RetryTask:
                session.MarkIdle(failure.Reason);
                break;
            case RuntimeFailureAction.RebuildWorktree:
            case RuntimeFailureAction.SwitchProvider:
                session.MarkIdle(failure.Reason, RuntimeActionability.WorkerActionable);
                break;
            case RuntimeFailureAction.AbortTask:
                session.Fail(failure.Reason);
                break;
            case RuntimeFailureAction.BlockTask:
            case RuntimeFailureAction.PauseSession:
            case RuntimeFailureAction.EscalateToOperator:
                session.Pause(failure.Reason);
                break;
        }
    }

    private static string DescribeRecoveryConsequence(WorkerRecoveryDecision decision)
    {
        return decision.Action switch
        {
            WorkerRecoveryAction.Retry => "Task will return to dispatchable retry state.",
            WorkerRecoveryAction.RebuildWorktree => "Task will retry after quarantining the current worktree and rebuilding a fresh workspace.",
            WorkerRecoveryAction.SwitchProvider => $"Task will switch to backend '{decision.AlternateBackendId ?? "(unknown)"}' before retrying.",
            WorkerRecoveryAction.BlockTask => "Task will be blocked for operator review.",
            WorkerRecoveryAction.EscalateToOperator => "Runtime will pause for operator intervention.",
            _ => "No recovery action was selected.",
        };
    }

    private TaskNode? TryGetTask(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return null;
        }

        var graph = taskGraphService.Load();
        return graph.Tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    private static TaskNode CloneTaskWithMetadata(TaskNode task, string key, string value)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            [key] = value,
        };

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
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };
    }
}
