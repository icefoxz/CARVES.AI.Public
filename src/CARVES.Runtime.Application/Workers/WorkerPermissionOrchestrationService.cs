using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerPermissionOrchestrationService
{
    private readonly string repoRoot;
    private readonly WorkerPermissionInterpreter interpreter;
    private readonly ApprovalPolicyEngine approvalPolicyEngine;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly IWorkerPermissionAuditRepository auditRepository;
    private readonly PlatformGovernanceService platformGovernanceService;
    private readonly RepoRegistryService repoRegistryService;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IMarkdownSyncService markdownSyncService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly PlannerWakeBridgeService plannerWakeBridgeService;

    public WorkerPermissionOrchestrationService(
        string repoRoot,
        WorkerPermissionInterpreter interpreter,
        ApprovalPolicyEngine approvalPolicyEngine,
        IRuntimeArtifactRepository artifactRepository,
        IWorkerPermissionAuditRepository auditRepository,
        PlatformGovernanceService platformGovernanceService,
        RepoRegistryService repoRegistryService,
        TaskGraphService taskGraphService,
        IRuntimeSessionRepository sessionRepository,
        IMarkdownSyncService markdownSyncService,
        RuntimeIncidentTimelineService incidentTimelineService,
        PlannerWakeBridgeService plannerWakeBridgeService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.interpreter = interpreter;
        this.approvalPolicyEngine = approvalPolicyEngine;
        this.artifactRepository = artifactRepository;
        this.auditRepository = auditRepository;
        this.platformGovernanceService = platformGovernanceService;
        this.repoRegistryService = repoRegistryService;
        this.taskGraphService = taskGraphService;
        this.sessionRepository = sessionRepository;
        this.markdownSyncService = markdownSyncService;
        this.incidentTimelineService = incidentTimelineService;
        this.plannerWakeBridgeService = plannerWakeBridgeService;
    }

    public WorkerExecutionResult Evaluate(WorkerRequest request, WorkerExecutionResult result)
    {
        var permissionRequests = interpreter.Interpret(request, result).ToList();
        if (permissionRequests.Count == 0)
        {
            return result;
        }

        var repoId = ResolveRepoId(request.Selection?.RepoId);
        var providerIsAwaitingApproval = result.Status == WorkerExecutionStatus.ApprovalWait;
        var auditRecords = auditRepository.Load().ToList();
        foreach (var permissionRequest in permissionRequests)
        {
            incidentTimelineService.AppendPermissionAudit(CreateAudit(
                auditRecords,
                repoId,
                permissionRequest,
                WorkerPermissionAuditEventKind.RequestObserved,
                decision: null,
                actorKind: WorkerPermissionDecisionActorKind.System,
                actorIdentity: "interpreter",
                reasonCode: "permission_observed",
                reason: permissionRequest.Summary,
                consequence: "Permission request was observed and normalized."));

            var policyDecision = approvalPolicyEngine.Evaluate(request, permissionRequest);
            permissionRequest.RecommendedDecision = policyDecision.Decision;
            permissionRequest.RecommendedReasonCode = policyDecision.ReasonCode;
            permissionRequest.RecommendedReason = policyDecision.Reason;
            permissionRequest.RecommendedConsequenceSummary = policyDecision.ConsequenceSummary;

            switch (policyDecision.Decision)
            {
                case WorkerPermissionDecision.Allow:
                    if (providerIsAwaitingApproval)
                    {
                        permissionRequest.State = WorkerPermissionState.Pending;
                        incidentTimelineService.AppendPermissionAudit(CreateAudit(
                            auditRecords,
                            repoId,
                            permissionRequest,
                            WorkerPermissionAuditEventKind.EscalatedForReview,
                            WorkerPermissionDecision.Allow,
                            WorkerPermissionDecisionActorKind.Policy,
                            $"policy:{request.ExecutionRequest?.Profile.ProfileId ?? "untrusted-default"}",
                            policyDecision.ReasonCode,
                            policyDecision.Reason,
                            "Policy recommends allow, but the worker is paused pending an explicit approval response."));
                        platformGovernanceService.RecordEvent(GovernanceEventType.WorkerPermissionEscalated, repoId, $"{policyDecision.Reason} Worker is awaiting an approval response.");
                    }
                    else
                    {
                        permissionRequest.State = WorkerPermissionState.Allowed;
                        permissionRequest.FinalDecision = WorkerPermissionDecision.Allow;
                        permissionRequest.DecisionActorKind = WorkerPermissionDecisionActorKind.Policy;
                        permissionRequest.DecisionIdentity = $"policy:{request.ExecutionRequest?.Profile.ProfileId ?? "untrusted-default"}";
                        permissionRequest.DecisionReason = policyDecision.Reason;
                        permissionRequest.ConsequenceSummary = policyDecision.ConsequenceSummary;
                        permissionRequest.ResolvedAt = DateTimeOffset.UtcNow;
                        incidentTimelineService.AppendPermissionAudit(CreateAudit(
                            auditRecords,
                            repoId,
                            permissionRequest,
                            WorkerPermissionAuditEventKind.PolicyAllowed,
                            WorkerPermissionDecision.Allow,
                            WorkerPermissionDecisionActorKind.Policy,
                            permissionRequest.DecisionIdentity,
                            policyDecision.ReasonCode,
                            policyDecision.Reason,
                            policyDecision.ConsequenceSummary));
                        platformGovernanceService.RecordEvent(GovernanceEventType.WorkerPermissionAllowed, repoId, policyDecision.Reason);
                    }
                    break;
                case WorkerPermissionDecision.Deny:
                    permissionRequest.State = WorkerPermissionState.Denied;
                    permissionRequest.FinalDecision = WorkerPermissionDecision.Deny;
                    permissionRequest.DecisionActorKind = WorkerPermissionDecisionActorKind.Policy;
                    permissionRequest.DecisionIdentity = $"policy:{request.ExecutionRequest?.Profile.ProfileId ?? "untrusted-default"}";
                    permissionRequest.DecisionReason = policyDecision.Reason;
                    permissionRequest.ConsequenceSummary = policyDecision.ConsequenceSummary;
                    permissionRequest.ResolvedAt = DateTimeOffset.UtcNow;
                    incidentTimelineService.AppendPermissionAudit(CreateAudit(
                        auditRecords,
                        repoId,
                        permissionRequest,
                        WorkerPermissionAuditEventKind.PolicyDenied,
                        WorkerPermissionDecision.Deny,
                        WorkerPermissionDecisionActorKind.Policy,
                        permissionRequest.DecisionIdentity,
                        policyDecision.ReasonCode,
                        policyDecision.Reason,
                        policyDecision.ConsequenceSummary));
                    platformGovernanceService.RecordEvent(GovernanceEventType.WorkerPermissionDenied, repoId, policyDecision.Reason);
                    break;
                default:
                    permissionRequest.State = WorkerPermissionState.Pending;
                    incidentTimelineService.AppendPermissionAudit(CreateAudit(
                        auditRecords,
                        repoId,
                        permissionRequest,
                        WorkerPermissionAuditEventKind.EscalatedForReview,
                        WorkerPermissionDecision.Review,
                        WorkerPermissionDecisionActorKind.Policy,
                        $"policy:{request.ExecutionRequest?.Profile.ProfileId ?? "untrusted-default"}",
                        policyDecision.ReasonCode,
                        policyDecision.Reason,
                        policyDecision.ConsequenceSummary));
                    platformGovernanceService.RecordEvent(GovernanceEventType.WorkerPermissionEscalated, repoId, policyDecision.Reason);
                    break;
            }
        }

        artifactRepository.SaveWorkerPermissionArtifact(new WorkerPermissionArtifact
        {
            TaskId = request.Task.TaskId,
            RunId = result.RunId,
            Requests = permissionRequests,
        });
        auditRepository.Save(auditRecords);

        var pending = permissionRequests.Where(item => item.State == WorkerPermissionState.Pending).ToArray();
        if (pending.Length > 0)
        {
            var summary = pending.Length == 1
                ? $"Worker permission request '{pending[0].PermissionRequestId}' is awaiting operator approval."
                : $"{pending.Length} worker permission requests are awaiting operator approval.";
            return result with
            {
                Status = WorkerExecutionStatus.ApprovalWait,
                FailureKind = WorkerFailureKind.ApprovalRequired,
                Retryable = false,
                Summary = summary,
                FailureReason = summary,
                PermissionRequests = permissionRequests,
            };
        }

        var denied = permissionRequests.FirstOrDefault(item => item.State == WorkerPermissionState.Denied);
        if (denied is not null)
        {
            return result with
            {
                Status = WorkerExecutionStatus.Blocked,
                FailureKind = WorkerFailureKind.PolicyDenied,
                Retryable = false,
                Summary = denied.DecisionReason ?? denied.Summary,
                FailureReason = denied.DecisionReason ?? denied.Summary,
                PermissionRequests = permissionRequests,
            };
        }

        return result with
        {
            PermissionRequests = permissionRequests,
        };
    }

    public IReadOnlyList<WorkerPermissionRequest> ListPendingRequests()
    {
        return artifactRepository.LoadWorkerPermissionArtifacts()
            .SelectMany(artifact => artifact.Requests)
            .Where(item => item.State == WorkerPermissionState.Pending)
            .OrderBy(item => item.RequestedAt)
            .ToArray();
    }

    public IReadOnlyList<WorkerPermissionAuditRecord> LoadAudit(string? taskId = null, string? permissionRequestId = null)
    {
        return auditRepository.Load()
            .Where(record => string.IsNullOrWhiteSpace(taskId) || string.Equals(record.TaskId, taskId, StringComparison.Ordinal))
            .Where(record => string.IsNullOrWhiteSpace(permissionRequestId) || string.Equals(record.PermissionRequestId, permissionRequestId, StringComparison.Ordinal))
            .OrderByDescending(record => record.OccurredAt)
            .ToArray();
    }

    public WorkerPermissionRequest ResolveApprove(string permissionRequestId, string actorIdentity)
    {
        return ResolveDecision(permissionRequestId, actorIdentity, WorkerPermissionDecision.Allow, WorkerPermissionState.Allowed, WorkerPermissionAuditEventKind.HumanAllowed, "operator_allowed", "Operator approved the permission request.", RuntimeActionability.WorkerActionable, GovernanceEventType.WorkerPermissionAllowed);
    }

    public WorkerPermissionRequest ResolveDeny(string permissionRequestId, string actorIdentity)
    {
        return ResolveDecision(permissionRequestId, actorIdentity, WorkerPermissionDecision.Deny, WorkerPermissionState.Denied, WorkerPermissionAuditEventKind.HumanDenied, "operator_denied", "Operator denied the permission request.", RuntimeActionability.HumanActionable, GovernanceEventType.WorkerPermissionDenied);
    }

    public WorkerPermissionRequest ResolveTimeout(string permissionRequestId, string actorIdentity)
    {
        return ResolveDecision(permissionRequestId, actorIdentity, decision: null, WorkerPermissionState.TimedOut, WorkerPermissionAuditEventKind.TimedOut, "operator_timeout", "Operator let the permission request time out.", RuntimeActionability.HumanActionable, GovernanceEventType.WorkerPermissionTimedOut);
    }

    private WorkerPermissionRequest ResolveDecision(
        string permissionRequestId,
        string actorIdentity,
        WorkerPermissionDecision? decision,
        WorkerPermissionState state,
        WorkerPermissionAuditEventKind auditEventKind,
        string reasonCode,
        string reason,
        RuntimeActionability actionability,
        GovernanceEventType governanceEventType)
    {
        var artifact = artifactRepository.LoadWorkerPermissionArtifacts()
            .FirstOrDefault(item => item.Requests.Any(request => string.Equals(request.PermissionRequestId, permissionRequestId, StringComparison.Ordinal)))
            ?? throw new InvalidOperationException($"Permission request '{permissionRequestId}' was not found.");
        var requests = artifact.Requests.ToList();
        var permissionRequest = requests.First(request => string.Equals(request.PermissionRequestId, permissionRequestId, StringComparison.Ordinal));
        permissionRequest.State = state;
        permissionRequest.FinalDecision = decision;
        permissionRequest.DecisionActorKind = WorkerPermissionDecisionActorKind.Human;
        permissionRequest.DecisionIdentity = actorIdentity;
        permissionRequest.DecisionReason = reason;
        permissionRequest.ConsequenceSummary = decision == WorkerPermissionDecision.Allow
            ? "Task returns to dispatchable execution."
            : state == WorkerPermissionState.TimedOut
                ? "Task remains blocked until a new worker run is requested."
                : "Task remains blocked after permission denial.";
        permissionRequest.ResolvedAt = DateTimeOffset.UtcNow;

        artifactRepository.SaveWorkerPermissionArtifact(new WorkerPermissionArtifact
        {
            TaskId = artifact.TaskId,
            RunId = artifact.RunId,
            Requests = requests,
        });

        var repoId = ResolveRepoId(null);
        var auditRecords = auditRepository.Load().ToList();
        incidentTimelineService.AppendPermissionAudit(CreateAudit(
            auditRecords,
            repoId,
            permissionRequest,
            auditEventKind,
            decision,
            WorkerPermissionDecisionActorKind.Human,
            actorIdentity,
            reasonCode,
            reason,
            permissionRequest.ConsequenceSummary ?? reason));
        if (decision == WorkerPermissionDecision.Allow)
        {
            incidentTimelineService.AppendPermissionAudit(CreateAudit(
                auditRecords,
                repoId,
                permissionRequest,
                WorkerPermissionAuditEventKind.ReturnedToDispatchable,
                WorkerPermissionDecision.Allow,
                WorkerPermissionDecisionActorKind.System,
                "runtime",
                "returned_to_dispatchable",
                "Task returned to dispatchable execution after approval.",
                "Task is pending a new worker run."));
        }

        auditRepository.Save(auditRecords);
        platformGovernanceService.RecordEvent(governanceEventType, repoId, $"{reason} [{permissionRequest.PermissionRequestId}]");
        ApplyTaskAndSessionDecision(permissionRequest, actionability);
        return permissionRequest;
    }

    private void ApplyTaskAndSessionDecision(WorkerPermissionRequest permissionRequest, RuntimeActionability actionability)
    {
        var task = taskGraphService.GetTask(permissionRequest.TaskId);
        switch (permissionRequest.State)
        {
            case WorkerPermissionState.Allowed:
                task.SetPlannerReview(new PlannerReview
                {
                    Verdict = PlannerVerdict.Continue,
                    Reason = permissionRequest.DecisionReason ?? "Permission request approved.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions = [$"Resume execution for {permissionRequest.TaskId}."],
                });
                task.ClearRetryBackoff();
                task.SetStatus(Carves.Runtime.Domain.Tasks.TaskStatus.Pending);
                break;
            case WorkerPermissionState.Denied:
            case WorkerPermissionState.TimedOut:
                task.SetPlannerReview(new PlannerReview
                {
                    Verdict = PlannerVerdict.Blocked,
                    Reason = permissionRequest.DecisionReason ?? "Permission request denied.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions = [$"Inspect permission request {permissionRequest.PermissionRequestId} before retrying."],
                });
                task.SetStatus(Carves.Runtime.Domain.Tasks.TaskStatus.Blocked);
                break;
        }

        taskGraphService.ReplaceTask(task);

        var session = sessionRepository.Load();
        if (session is null)
        {
            return;
        }

        session.ResolvePermissionRequest(permissionRequest.PermissionRequestId, permissionRequest.DecisionReason ?? permissionRequest.Summary, actionability);
        plannerWakeBridgeService.Queue(
            session,
            PlannerWakeReason.ApprovalResolved,
            PlannerWakeSourceKind.PermissionResolution,
            $"Permission request {permissionRequest.PermissionRequestId} resolved for task {permissionRequest.TaskId}: {permissionRequest.DecisionReason ?? permissionRequest.Summary}",
            $"{permissionRequest.TaskId}: permission {permissionRequest.State}",
            permissionRequest.TaskId,
            permissionRequest.RunId);
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
    }

    private string ResolveRepoId(string? repoId)
    {
        if (!string.IsNullOrWhiteSpace(repoId))
        {
            return repoId;
        }

        return repoRegistryService.List()
            .FirstOrDefault(item => string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase))
            ?.RepoId
            ?? "local-repo";
    }

    private static WorkerPermissionAuditRecord CreateAudit(
        ICollection<WorkerPermissionAuditRecord> records,
        string repoId,
        WorkerPermissionRequest permissionRequest,
        WorkerPermissionAuditEventKind eventKind,
        WorkerPermissionDecision? decision,
        WorkerPermissionDecisionActorKind actorKind,
        string actorIdentity,
        string reasonCode,
        string reason,
        string consequence)
    {
        var record = new WorkerPermissionAuditRecord
        {
            RepoId = repoId,
            PermissionRequestId = permissionRequest.PermissionRequestId,
            RunId = permissionRequest.RunId,
            TaskId = permissionRequest.TaskId,
            BackendId = permissionRequest.BackendId,
            ProviderId = permissionRequest.ProviderId,
            PermissionKind = permissionRequest.Kind,
            RiskLevel = permissionRequest.RiskLevel,
            EventKind = eventKind,
            Decision = decision,
            ActorKind = actorKind,
            ActorIdentity = actorIdentity,
            ReasonCode = reasonCode,
            Reason = reason,
            ConsequenceSummary = consequence,
        };
        records.Add(record);
        return record;
    }
}
