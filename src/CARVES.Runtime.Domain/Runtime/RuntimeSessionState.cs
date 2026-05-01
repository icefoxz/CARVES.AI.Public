using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Domain.Runtime;

public sealed class RuntimeSessionState
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string SessionId { get; init; } = "default";

    public string AttachedRepoRoot { get; init; } = string.Empty;

    public RuntimeSessionStatus Status { get; set; } = RuntimeSessionStatus.Idle;

    public RuntimeLoopMode LoopMode { get; set; } = RuntimeLoopMode.ManualTick;

    public bool DryRun { get; set; }

    public int ActiveWorkerCount { get; set; }

    public int TickCount { get; set; }

    public IReadOnlyList<string> ActiveTaskIds { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> ReviewPendingTaskIds { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> PendingPermissionRequestIds { get; set; } = Array.Empty<string>();

    public string? CurrentTaskId { get; set; }

    public string? LastTaskId { get; set; }

    public string? LastReviewTaskId { get; set; }

    public string LastReason { get; set; } = "Session has not started.";

    public string LoopReason { get; set; } = "Session has not started.";

    public RuntimeActionability LoopActionability { get; set; } = RuntimeActionability.WorkerActionable;

    public string? WaitingReason { get; set; }

    public RuntimeActionability WaitingActionability { get; set; } = RuntimeActionability.None;

    public string? StopReason { get; set; }

    public RuntimeActionability StopActionability { get; set; } = RuntimeActionability.None;

    public string? LastPlannerReentryOutcome { get; set; }

    public IReadOnlyList<string> LastPlannerReentryTaskIds { get; set; } = Array.Empty<string>();

    public int PlannerRound { get; set; }

    public PlannerLifecycleState PlannerLifecycleState { get; set; } = PlannerLifecycleState.Idle;

    public PlannerSleepReason PlannerSleepReason { get; set; } = PlannerSleepReason.None;

    public PlannerWakeReason PlannerWakeReason { get; set; } = PlannerWakeReason.None;

    public PlannerEscalationReason PlannerEscalationReason { get; set; } = PlannerEscalationReason.None;

    public string? PlannerLifecycleReason { get; set; }

    public string? PlannerAdapterId { get; set; }

    public string? PlannerProposalId { get; set; }

    public string? PlannerLeaseId { get; set; }

    public bool PlannerLeaseActive { get; set; }

    public PlannerLeaseMode PlannerLeaseMode { get; set; } = PlannerLeaseMode.None;

    public string? PlannerLeaseOwner { get; set; }

    public string? PlannerLeaseReason { get; set; }

    public DateTimeOffset? PlannerLeaseAcquiredAt { get; set; }

    public DateTimeOffset? PlannerLeaseReleasedAt { get; set; }

    public IReadOnlyList<PlannerWakeSignal> PendingPlannerWakeSignals { get; set; } = Array.Empty<PlannerWakeSignal>();

    public string? LastConsumedPlannerWakeSignalId { get; set; }

    public string? LastConsumedPlannerWakeSummary { get; set; }

    public int DetectedOpportunityCount { get; set; }

    public int EvaluatedOpportunityCount { get; set; }

    public string? LastOpportunitySource { get; set; }

    public string? AnalysisReason { get; set; }

    public string? LastWorkerRunId { get; set; }

    public string? LastWorkerBackend { get; set; }

    public WorkerFailureKind LastWorkerFailureKind { get; set; } = WorkerFailureKind.None;

    public string? LastWorkerSummary { get; set; }

    public WorkerRecoveryAction LastRecoveryAction { get; set; } = WorkerRecoveryAction.None;

    public string? LastRecoveryReason { get; set; }

    public string? LastPermissionRequestId { get; set; }

    public string? LastPermissionSummary { get; set; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastTickAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public RuntimeActionability CurrentActionability => RuntimeActionabilitySemantics.ResolveCurrent(this);

    public static RuntimeSessionState Start(string attachedRepoRoot, bool dryRun)
    {
        var session = new RuntimeSessionState
        {
            AttachedRepoRoot = attachedRepoRoot,
        };

        session.DryRun = dryRun;
        session.LastReason = "Session attached and idle.";
        session.LoopReason = session.LastReason;
        session.LoopActionability = RuntimeActionability.WorkerActionable;
        return session;
    }

    public void BeginTick(bool dryRun, RuntimeLoopMode loopMode = RuntimeLoopMode.ManualTick)
    {
        DryRun = dryRun;
        LoopMode = loopMode;
        TickCount += 1;
        LastTickAt = DateTimeOffset.UtcNow;
        SetLoopState(
            loopMode == RuntimeLoopMode.ContinuousLoop
                ? "Continuous loop iteration started."
                : "Manual tick started.",
            RuntimeActionability.WorkerActionable);
        ClearWaitingState();
        ClearStopState();
        Touch();
    }

    public void MarkScheduling(string reason)
    {
        Status = RuntimeSessionStatus.Scheduling;
        ClearWaitingState();
        ClearStopState();
        SetLoopState(reason, RuntimeActionability.WorkerActionable);
        Touch();
    }

    public void MarkExecuting(string taskId, string reason)
    {
        MarkExecuting([taskId], reason);
    }

    public void MarkExecuting(IReadOnlyList<string> taskIds, string reason)
    {
        Status = RuntimeSessionStatus.Executing;
        ActiveTaskIds = taskIds.Distinct(StringComparer.Ordinal).ToArray();
        CurrentTaskId = ActiveTaskIds.FirstOrDefault();
        LastTaskId = ActiveTaskIds.LastOrDefault();
        ClearWaitingState();
        ClearStopState();
        SetLoopState(reason, RuntimeActionability.WorkerActionable);
        Touch();
    }

    public void MarkReviewWait(string taskId, string reason)
    {
        if (!ReviewPendingTaskIds.Contains(taskId, StringComparer.Ordinal))
        {
            ReviewPendingTaskIds = ReviewPendingTaskIds.Concat([taskId]).ToArray();
        }

        Status = RuntimeSessionStatus.ReviewWait;
        CurrentTaskId = null;
        ActiveTaskIds = ActiveTaskIds.Where(activeTaskId => !string.Equals(activeTaskId, taskId, StringComparison.Ordinal)).ToArray();
        LastTaskId = taskId;
        LastReviewTaskId = taskId;
        SetWaitingState(reason, RuntimeActionability.HumanActionable);
        ClearStopState();
        SetLoopState(reason, RuntimeActionability.HumanActionable);
        Touch();
    }

    public void MarkApprovalWait(string taskId, IReadOnlyList<string> permissionRequestIds, string reason)
    {
        Status = RuntimeSessionStatus.ApprovalWait;
        ActiveTaskIds = ActiveTaskIds.Where(activeTaskId => !string.Equals(activeTaskId, taskId, StringComparison.Ordinal)).ToArray();
        CurrentTaskId = null;
        LastTaskId = taskId;
        PendingPermissionRequestIds = permissionRequestIds.Distinct(StringComparer.Ordinal).ToArray();
        LastPermissionRequestId = PendingPermissionRequestIds.LastOrDefault();
        LastPermissionSummary = reason;
        SetWaitingState(reason, RuntimeActionability.HumanActionable);
        ClearStopState();
        SetLoopState(reason, RuntimeActionability.HumanActionable);
        Touch();
    }

    public void MarkIdle(string reason, RuntimeActionability actionability = RuntimeActionability.WorkerActionable)
    {
        Status = ReviewPendingTaskIds.Count == 0 ? RuntimeSessionStatus.Idle : RuntimeSessionStatus.ReviewWait;
        CurrentTaskId = null;
        if (Status == RuntimeSessionStatus.ReviewWait)
        {
            SetWaitingState(reason, RuntimeActionability.HumanActionable);
            SetLoopState(reason, RuntimeActionability.HumanActionable);
        }
        else
        {
            ClearWaitingState();
            SetLoopState(reason, actionability);
        }

        ClearStopState();
        Touch();
    }

    public void Pause(string reason, RuntimeActionability actionability = RuntimeActionability.HumanActionable)
    {
        Status = RuntimeSessionStatus.Paused;
        CurrentTaskId = null;
        SetWaitingState(reason, actionability);
        ClearStopState();
        SetLoopState(reason, actionability);
        Touch();
    }

    public void Resume(string reason)
    {
        Status = RuntimeSessionStatus.Idle;
        ClearWaitingState();
        ClearStopState();
        SetLoopState(reason, RuntimeActionability.WorkerActionable);
        Touch();
    }

    public void Stop(string reason, RuntimeActionability actionability = RuntimeActionability.Terminal)
    {
        Status = RuntimeSessionStatus.Stopped;
        ActiveWorkerCount = 0;
        ActiveTaskIds = Array.Empty<string>();
        CurrentTaskId = null;
        ClearWaitingState();
        SetStopState(reason, actionability);
        SetLoopState(reason, actionability);
        Touch();
    }

    public void Fail(string reason, RuntimeActionability actionability = RuntimeActionability.Terminal)
    {
        Status = RuntimeSessionStatus.Failed;
        ActiveTaskIds = Array.Empty<string>();
        CurrentTaskId = null;
        ClearWaitingState();
        SetStopState(reason, actionability);
        SetLoopState(reason, actionability);
        Touch();
    }

    public void AcquireWorker(string taskId)
    {
        ActiveWorkerCount += 1;
        if (!ActiveTaskIds.Contains(taskId, StringComparer.Ordinal))
        {
            ActiveTaskIds = ActiveTaskIds.Concat([taskId]).ToArray();
        }

        CurrentTaskId = ActiveTaskIds.FirstOrDefault();
        LastTaskId = taskId;
        Touch();
    }

    public void ReleaseWorker(string taskId)
    {
        ActiveWorkerCount = Math.Max(0, ActiveWorkerCount - 1);
        ActiveTaskIds = ActiveTaskIds.Where(activeTaskId => !string.Equals(activeTaskId, taskId, StringComparison.Ordinal)).ToArray();
        CurrentTaskId = ActiveTaskIds.FirstOrDefault();
        Touch();
    }

    public void ResolveReviewPending(string taskId, string reason)
    {
        ReviewPendingTaskIds = ReviewPendingTaskIds.Where(reviewTaskId => !string.Equals(reviewTaskId, taskId, StringComparison.Ordinal)).ToArray();
        if (Status is not RuntimeSessionStatus.Paused and not RuntimeSessionStatus.Failed and not RuntimeSessionStatus.Stopped)
        {
            Status = ReviewPendingTaskIds.Count == 0 ? RuntimeSessionStatus.Idle : RuntimeSessionStatus.ReviewWait;
        }

        CurrentTaskId = ActiveTaskIds.FirstOrDefault();
        LastReviewTaskId = ReviewPendingTaskIds.LastOrDefault() ?? LastReviewTaskId;
        if (ReviewPendingTaskIds.Count == 0)
        {
            ClearWaitingState();
            SetLoopState(reason, RuntimeActionability.WorkerActionable);
        }
        else
        {
            SetWaitingState(reason, RuntimeActionability.HumanActionable);
            SetLoopState(reason, RuntimeActionability.HumanActionable);
        }

        Touch();
    }

    public void ResolvePermissionRequest(string permissionRequestId, string reason, RuntimeActionability actionability)
    {
        PendingPermissionRequestIds = PendingPermissionRequestIds
            .Where(existing => !string.Equals(existing, permissionRequestId, StringComparison.Ordinal))
            .ToArray();
        LastPermissionRequestId = permissionRequestId;
        LastPermissionSummary = reason;

        if (PendingPermissionRequestIds.Count == 0)
        {
            Status = ReviewPendingTaskIds.Count == 0 ? RuntimeSessionStatus.Idle : RuntimeSessionStatus.ReviewWait;
            if (Status == RuntimeSessionStatus.ReviewWait)
            {
                SetWaitingState(reason, RuntimeActionability.HumanActionable);
                SetLoopState(reason, RuntimeActionability.HumanActionable);
            }
            else
            {
                ClearWaitingState();
                SetLoopState(reason, actionability);
            }
        }
        else
        {
            Status = RuntimeSessionStatus.ApprovalWait;
            SetWaitingState(reason, RuntimeActionability.HumanActionable);
            SetLoopState(reason, RuntimeActionability.HumanActionable);
        }

        Touch();
    }

    public void RecordPlannerReentry(
        string outcome,
        IReadOnlyList<string> taskIds,
        string reason,
        RuntimeActionability actionability = RuntimeActionability.PlannerActionable,
        int plannerRound = 0,
        int detectedOpportunityCount = 0,
        int evaluatedOpportunityCount = 0,
        string? opportunitySourceSummary = null,
        string? analysisReason = null)
    {
        LastPlannerReentryOutcome = outcome;
        LastPlannerReentryTaskIds = taskIds;
        if (plannerRound > 0)
        {
            PlannerRound = plannerRound;
        }

        DetectedOpportunityCount = detectedOpportunityCount;
        EvaluatedOpportunityCount = evaluatedOpportunityCount;
        LastOpportunitySource = opportunitySourceSummary;
        AnalysisReason = analysisReason ?? reason;
        SetLoopState(reason, actionability);
        Touch();
    }

    public void WakePlanner(PlannerWakeReason wakeReason, string reason)
    {
        PlannerLifecycleState = PlannerLifecycleState.Idle;
        PlannerWakeReason = wakeReason;
        PlannerSleepReason = PlannerSleepReason.None;
        PlannerEscalationReason = PlannerEscalationReason.None;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public PlannerWakeSignal EnqueuePlannerWake(
        PlannerWakeReason wakeReason,
        PlannerWakeSourceKind sourceKind,
        string detail,
        string summary,
        string? taskId = null,
        string? runId = null)
    {
        var signal = new PlannerWakeSignal
        {
            WakeReason = wakeReason,
            SourceKind = sourceKind,
            Detail = detail,
            Summary = summary,
            TaskId = taskId,
            RunId = runId,
        };

        PendingPlannerWakeSignals = PendingPlannerWakeSignals.Concat([signal]).ToArray();
        Touch();
        return signal;
    }

    public bool TryConsumePlannerWake(out PlannerWakeSignal? signal)
    {
        if (PendingPlannerWakeSignals.Count == 0)
        {
            signal = null;
            return false;
        }

        signal = PendingPlannerWakeSignals[0];
        PendingPlannerWakeSignals = PendingPlannerWakeSignals.Skip(1).ToArray();
        LastConsumedPlannerWakeSignalId = signal.SignalId;
        LastConsumedPlannerWakeSummary = signal.Summary;
        WakePlanner(signal.WakeReason, signal.Detail);
        return true;
    }

    public void AcquirePlannerLease(PlannerLeaseMode leaseMode, string owner, string reason)
    {
        PlannerLeaseId = $"planner-lease-{Guid.NewGuid():N}";
        PlannerLeaseActive = true;
        PlannerLeaseMode = leaseMode;
        PlannerLeaseOwner = owner;
        PlannerLeaseReason = reason;
        PlannerLeaseAcquiredAt = DateTimeOffset.UtcNow;
        PlannerLeaseReleasedAt = null;
        Touch();
    }

    public void ReleasePlannerLease(string reason)
    {
        PlannerLeaseActive = false;
        PlannerLeaseReason = reason;
        PlannerLeaseReleasedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void ActivatePlanner(string reason, string adapterId)
    {
        PlannerLifecycleState = PlannerLifecycleState.Active;
        PlannerSleepReason = PlannerSleepReason.None;
        PlannerEscalationReason = PlannerEscalationReason.None;
        PlannerLifecycleReason = reason;
        PlannerAdapterId = adapterId;
        Touch();
    }

    public void SleepPlanner(PlannerSleepReason sleepReason, string reason)
    {
        PlannerLifecycleState = PlannerLifecycleState.Sleeping;
        PlannerSleepReason = sleepReason;
        PlannerEscalationReason = PlannerEscalationReason.None;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public void WaitPlanner(PlannerSleepReason waitingReason, string reason)
    {
        PlannerLifecycleState = PlannerLifecycleState.Waiting;
        PlannerSleepReason = waitingReason;
        PlannerEscalationReason = PlannerEscalationReason.None;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public void BlockPlanner(PlannerEscalationReason escalationReason, string reason)
    {
        PlannerLifecycleState = PlannerLifecycleState.Blocked;
        PlannerEscalationReason = escalationReason;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public void EscalatePlanner(PlannerEscalationReason escalationReason, string reason)
    {
        PlannerLifecycleState = PlannerLifecycleState.Escalated;
        PlannerEscalationReason = escalationReason;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public void RecordPlannerProposal(string proposalId, string adapterId, string reason)
    {
        PlannerProposalId = proposalId;
        PlannerAdapterId = adapterId;
        PlannerLifecycleReason = reason;
        Touch();
    }

    public void RecordWorkerOutcome(WorkerExecutionResult result, string reason, RuntimeActionability actionability)
    {
        LastWorkerRunId = result.RunId;
        LastWorkerBackend = result.BackendId;
        LastWorkerFailureKind = result.FailureKind;
        LastWorkerSummary = result.Summary;
        if (result.PermissionRequests.Count > 0)
        {
            LastPermissionRequestId = result.PermissionRequests.Last().PermissionRequestId;
            LastPermissionSummary = result.PermissionRequests.Last().Summary;
        }
        SetLoopState(reason, actionability);
        Touch();
    }

    public void RecordRecoveryDecision(string? taskId, WorkerRecoveryDecision decision, bool updateLoopState = true)
    {
        LastTaskId = taskId ?? LastTaskId;
        LastRecoveryAction = decision.Action;
        LastRecoveryReason = decision.Reason;
        if (updateLoopState)
        {
            SetLoopState(decision.Reason, decision.Actionability);
        }

        Touch();
    }

    private void SetLoopState(string reason, RuntimeActionability actionability)
    {
        LoopReason = reason;
        LoopActionability = actionability;
        LastReason = reason;
    }

    private void SetWaitingState(string reason, RuntimeActionability actionability)
    {
        WaitingReason = reason;
        WaitingActionability = actionability;
    }

    private void ClearWaitingState()
    {
        WaitingReason = null;
        WaitingActionability = RuntimeActionability.None;
    }

    private void SetStopState(string reason, RuntimeActionability actionability)
    {
        StopReason = reason;
        StopActionability = actionability;
    }

    private void ClearStopState()
    {
        StopReason = null;
        StopActionability = RuntimeActionability.None;
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
