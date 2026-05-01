using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private DelegatedExecutionResultEnvelope BuildDelegatedExecutionResult(
        CycleResult cycle,
        ActorSessionRecord actorSession,
        bool manualFallback,
        ExecutionRun executionRun,
        DelegatedResultSubmissionOutcome resultSubmission)
    {
        var task = cycle.Task ?? throw new InvalidOperationException("Delegated execution cycle returned no task.");
        var session = cycle.Session ?? devLoopService.GetSession();
        var worker = cycle.Report?.WorkerExecution;
        var exitCode = worker?.CommandTrace.LastOrDefault(item => !item.Skipped)?.ExitCode;
        var durationMs = worker is null
            ? (long?)null
            : Math.Max(0, (long)(worker.CompletedAt - worker.StartedAt).TotalMilliseconds);
        return new DelegatedExecutionResultEnvelope
        {
            TaskId = task.TaskId,
            Accepted = true,
            Outcome = ResolveDelegationOutcome(task, worker),
            ActorKind = actorSession.Kind.ToString(),
            ActorIdentity = actorSession.ActorIdentity,
            ManualFallback = manualFallback,
            RuntimeSessionId = session?.SessionId,
            TaskStatus = task.Status.ToString(),
            SessionStatus = session?.Status.ToString() ?? "none",
            ProviderId = worker?.ProviderId,
            BackendId = worker?.BackendId,
            ProfileId = worker?.ProfileId,
            RunId = worker?.RunId,
            ExecutionRunId = executionRun.RunId,
            ResultEnvelopePath = resultSubmission.ResultEnvelopePath,
            HostResultIngestionAttempted = resultSubmission.IngestionOutcome is not null,
            HostResultIngestionApplied = resultSubmission.IngestionOutcome is { AlreadyApplied: false },
            HostResultIngestionAlreadyApplied = resultSubmission.IngestionOutcome?.AlreadyApplied ?? false,
            ResultSubmissionStatus = resultSubmission.Status,
            SafetyArtifactPresent = resultSubmission.SafetyGate.ArtifactPresent,
            SafetyGateStatus = resultSubmission.SafetyGate.Status,
            SafetyGateAllowed = resultSubmission.SafetyGate.Allowed,
            SafetyGateIssues = resultSubmission.SafetyGate.Issues,
            ReviewSubmissionPath = resultSubmission.IngestionOutcome?.ReviewSubmissionPath,
            EffectLedgerPath = resultSubmission.IngestionOutcome?.EffectLedgerPath,
            ResultCommit = resultSubmission.IngestionOutcome?.ResultCommit,
            ExitCode = exitCode,
            DurationMs = durationMs,
            Summary = worker?.Summary ?? cycle.Message,
            FailureKind = worker is null || worker.FailureKind == WorkerFailureKind.None ? null : worker.FailureKind.ToString(),
            Retryable = worker?.Retryable ?? false,
            ChangedFiles = worker?.ChangedFiles ?? Array.Empty<string>(),
            NextAction = ResolveDelegatedNextAction(task),
            Guidance = BuildDelegationGuidance(task, worker, manualFallback),
            FallbackRunPacket = BuildFallbackRunPacket(task.TaskId, actorSession, manualFallback, worker, worker?.RunId),
        };
    }

    private DelegatedExecutionResultEnvelope BuildExistingDelegatedExecutionResult(
        TaskNode task,
        ActorSessionRecord actorSession,
        bool manualFallback,
        RuntimeSessionState? session,
        ExecutionRun executionRun)
    {
        return new DelegatedExecutionResultEnvelope
        {
            TaskId = task.TaskId,
            Accepted = true,
            Outcome = "already_running",
            ActorKind = actorSession.Kind.ToString(),
            ActorIdentity = actorSession.ActorIdentity,
            ManualFallback = manualFallback,
            RuntimeSessionId = session?.SessionId,
            TaskStatus = task.Status.ToString(),
            SessionStatus = session?.Status.ToString() ?? "none",
            ExecutionRunId = executionRun.RunId,
            Summary = $"Delegated execution is already active for {task.TaskId} under execution run {executionRun.RunId}.",
            NextAction = $"Inspect task {task.TaskId} and the active execution run before retrying delegated execution.",
            FallbackRunPacket = BuildFallbackRunPacket(task.TaskId, actorSession, manualFallback, worker: null, executionRun.RunId),
            Guidance =
            [
                $"Use `task inspect {task.TaskId} --runs` to inspect the authoritative active execution run.",
                $"Wait for execution run {executionRun.RunId} to settle before retrying `task run {task.TaskId}`."
            ],
        };
    }

    private FallbackRunPacket? BuildFallbackRunPacket(
        string taskId,
        ActorSessionRecord actorSession,
        bool manualFallback,
        WorkerExecutionResult? worker,
        string? runId)
    {
        if (!manualFallback)
        {
            return null;
        }

        var roleSwitchReceiptRef = ResolveFallbackRoleSwitchReceiptRef(actorSession);
        var contextReceiptRef = string.IsNullOrWhiteSpace(actorSession.LastContextReceipt)
            ? null
            : actorSession.LastContextReceipt;
        var executionClaimRef = ResolveExecutionClaimRef(worker);
        var reviewBundleRef = artifactRepository.TryLoadPlannerReviewArtifact(taskId) is null
            ? null
            : $".ai/artifacts/reviews/{taskId}.json#closure_bundle";
        var receipts = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["role_switch_receipt"] = roleSwitchReceiptRef,
            ["context_receipt"] = contextReceiptRef,
            ["execution_claim"] = executionClaimRef,
            ["review_bundle"] = reviewBundleRef,
        };
        var present = receipts
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key)
            .ToArray();
        var missing = receipts
            .Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key)
            .ToArray();

        return new FallbackRunPacket
        {
            TaskId = taskId,
            RunId = runId,
            ActorSessionId = actorSession.ActorSessionId,
            ActorKind = actorSession.Kind.ToString(),
            ActorIdentity = actorSession.ActorIdentity,
            Trigger = "manual_or_same_agent_fallback",
            Status = missing.Length == 0 ? "complete" : "incomplete",
            StrictlyRequired = true,
            ClosureBlockerWhenIncomplete = true,
            RequiredReceipts = receipts.Keys.ToArray(),
            PresentReceipts = present,
            MissingReceipts = missing,
            RoleSwitchReceiptRef = roleSwitchReceiptRef,
            ContextReceiptRef = contextReceiptRef,
            ExecutionClaimRef = executionClaimRef,
            ReviewBundleRef = reviewBundleRef,
            GrantsExecutionAuthority = false,
            GrantsTruthWriteAuthority = false,
            CreatesTaskQueue = false,
            Notes =
            [
                "Fallback run packet is required evidence for fallback execution. It is not lifecycle truth and does not bypass Review or writeback gates.",
                "Missing receipts must be resolved before claiming fallback closure.",
            ],
        };
    }

    private static string? ResolveFallbackRoleSwitchReceiptRef(ActorSessionRecord actorSession)
    {
        return actorSession.Kind == ActorSessionKind.Agent
            ? $"actor_session:{actorSession.ActorSessionId}:same_agent_role_switch_required"
            : null;
    }

    private static string? ResolveExecutionClaimRef(WorkerExecutionResult? worker)
    {
        if (worker is null || !worker.CompletionClaim.Required)
        {
            return null;
        }

        return $"worker_completion_claim:{worker.RunId}:{worker.CompletionClaim.Status}";
    }

    private static string ResolveDelegationOutcome(TaskNode task, WorkerExecutionResult? worker)
    {
        if (task.Status == DomainTaskStatus.Review)
        {
            return "review";
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return "approval_wait";
        }

        if (task.Status == DomainTaskStatus.Completed || task.Status == DomainTaskStatus.Merged)
        {
            return "completed";
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return "blocked";
        }

        if (task.Status == DomainTaskStatus.Pending && worker is not null && !worker.Succeeded)
        {
            return worker.Retryable ? "retry_pending" : "pending";
        }

        return task.Status.ToString().ToLowerInvariant();
    }

    private static string ResolveDelegatedNextAction(TaskNode task)
    {
        return task.Status switch
        {
            DomainTaskStatus.Review => $"Resolve review for {task.TaskId}.",
            DomainTaskStatus.ApprovalWait => $"Resolve permission approval for {task.TaskId}.",
            DomainTaskStatus.Blocked => $"Inspect {task.TaskId} and request planner/operator follow-up.",
            DomainTaskStatus.Pending => $"Inspect {task.TaskId} before the next delegated execution attempt.",
            DomainTaskStatus.Completed or DomainTaskStatus.Merged => $"Review downstream work linked to {task.TaskId}.",
            _ => $"Inspect {task.TaskId}.",
        };
    }

    private static IReadOnlyList<string> BuildDelegationGuidance(TaskNode task, WorkerExecutionResult? worker, bool manualFallback)
    {
        var guidance = new List<string>();
        if (manualFallback)
        {
            guidance.Add("This run used explicit cold-path fallback; prefer `host ensure --json` before future delegated execution.");
        }

        guidance.Add($"Inspect authoritative task truth with `task inspect {task.TaskId}`.");
        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            guidance.Add("Use `worker approvals` and `worker approve|deny|timeout <permission-request-id>` before retrying.");
        }
        else if (task.Status == DomainTaskStatus.Review)
        {
            guidance.Add("Use review resolution commands before attempting more execution.");
        }
        else if (worker is not null && !worker.Succeeded && worker.Retryable)
        {
            guidance.Add($"Retry through `task run {task.TaskId}` after reviewing the worker summary.");
        }

        if (worker?.FailureKind == WorkerFailureKind.Timeout && !string.IsNullOrWhiteSpace(worker.TimeoutPhase))
        {
            if (string.Equals(worker.TimeoutPhase, "execution_started", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(worker.TimeoutEvidence)
                && worker.TimeoutEvidence.Contains("Non-converging read-only control-plane rescan detected", StringComparison.Ordinal))
            {
                guidance.Add("The delegated worker kept re-scanning task/card truth without making changes; inspect the direct authoritative task/card paths already provided to the worker and avoid broad `.ai/tasks/cards` or `.ai/tasks/nodes` rediscovery before retrying.");
            }

            guidance.Add(worker.TimeoutPhase switch
            {
                "before_first_cli_event" => "The timeout occurred before the first meaningful CLI event; inspect the delegated launch contract and CLI bootstrap path before retrying.",
                "bootstrap" => "The timeout occurred during CLI bootstrap; inspect early CLI events and wrapper startup output before retrying.",
                "execution_started" => "The timeout occurred after execution activity began; inspect worker command trace and stuck-run evidence before retrying.",
                _ => "Inspect timeout-phase evidence on the worker artifact before retrying.",
            });
        }

        return guidance;
    }

    private void AppendDelegationEvent(OperatorOsEventKind eventKind, ActorSessionRecord actorSession, string taskId, string? runId, string reasonCode, string summary)
    {
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = eventKind,
            RepoId = actorSession.RepoId,
            ActorSessionId = actorSession.ActorSessionId,
            ActorKind = actorSession.Kind,
            ActorIdentity = actorSession.ActorIdentity,
            TaskId = taskId,
            RunId = runId,
            OwnershipScope = OwnershipScope.TaskMutation,
            OwnershipTargetId = taskId,
            ReasonCode = reasonCode,
            Summary = summary,
        });
    }

    private static ActorSessionState ResolveDelegationActorState(CycleResult cycle)
    {
        if (cycle.Task?.Status == DomainTaskStatus.ApprovalWait)
        {
            return ActorSessionState.Waiting;
        }

        if (cycle.Task?.Status == DomainTaskStatus.Review || cycle.Task?.Status == DomainTaskStatus.Blocked)
        {
            return ActorSessionState.Blocked;
        }

        return ActorSessionState.Active;
    }

    private ExecutionRun FinalizeExecutionRun(TaskNode task, ExecutionRun run, string? resultEnvelopePath)
    {
        return task.Status switch
        {
            DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait => executionRunService.CompleteRun(run, resultEnvelopePath),
            DomainTaskStatus.Pending or DomainTaskStatus.Blocked or DomainTaskStatus.Failed => executionRunService.FailRun(run, resultEnvelopePath, task.PlannerReview.Reason),
            _ => run,
        };
    }
}
