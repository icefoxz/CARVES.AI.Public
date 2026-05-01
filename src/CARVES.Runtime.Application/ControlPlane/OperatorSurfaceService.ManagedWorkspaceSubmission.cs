using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult SubmitManagedWorkspaceForReview(string taskId, string? reason = null)
    {
        ManagedWorkspaceSubmissionCandidate candidate;
        try
        {
            candidate = managedWorkspaceLeaseService.BuildSubmissionCandidate(taskId);
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }

        var task = taskGraphService.GetTask(candidate.TaskId);
        if (task.Status != DomainTaskStatus.Pending)
        {
            return OperatorCommandResult.Failure($"Task {task.TaskId} is not in PENDING state and cannot submit a managed workspace result.");
        }

        var now = DateTimeOffset.UtcNow;
        var runId = $"managed-workspace-{task.TaskId}-{now:yyyyMMddHHmmss}";
        var summary = $"Managed workspace submitted {candidate.ChangedPaths.Count} file(s) for review.";
        var workerResult = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            RunId = runId,
            BackendId = "managed_workspace",
            ProviderId = "local",
            AdapterId = "ManagedWorkspaceLease",
            AdapterReason = summary,
            ProtocolFamily = "runtime_managed_workspace",
            RequestFamily = "mode_d_scoped_task_workspace",
            ProfileId = "mode_d_scoped_task_workspace",
            TrustedProfile = true,
            Status = WorkerExecutionStatus.Succeeded,
            FailureKind = WorkerFailureKind.None,
            FailureLayer = WorkerFailureLayer.None,
            Retryable = false,
            Configured = true,
            RequestPreview = $"submit managed workspace for {task.TaskId}",
            RequestHash = candidate.Lease.LeaseId,
            Summary = summary,
            Rationale = "Workspace result returned through Runtime-owned managed workspace submission.",
            ChangedFiles = candidate.ChangedPaths,
            StartedAt = candidate.Lease.CreatedAt,
            CompletedAt = now,
        };
        var evidence = new ExecutionEvidence
        {
            TaskId = task.TaskId,
            RunId = runId,
            WorkerId = "managed_workspace",
            StartedAt = candidate.Lease.CreatedAt,
            EndedAt = now,
            EvidenceSource = ExecutionEvidenceSource.Host,
            DeclaredScopeFiles = candidate.Lease.AllowedWritablePaths,
            FilesWritten = candidate.ChangedPaths,
            CommandsExecuted = [$"plan submit-workspace {task.TaskId}"],
            RepoRoot = repoRoot,
            WorktreePath = candidate.Lease.WorkspacePath,
            BaseCommit = candidate.Lease.BaseCommit,
            ExitStatus = 0,
            EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
            EvidenceStrength = ExecutionEvidenceStrength.Verifiable,
            Artifacts =
            [
                $"managed_workspace_lease:{candidate.Lease.LeaseId}",
            ],
        };
        var workerArtifact = new WorkerExecutionArtifact
        {
            TaskId = task.TaskId,
            Result = workerResult,
            Evidence = evidence,
        };
        artifactRepository.SaveWorkerExecutionArtifact(workerArtifact);

        var reviewReason = string.IsNullOrWhiteSpace(reason)
            ? $"Managed workspace lease {candidate.Lease.LeaseId} submitted for review."
            : reason.Trim();
        var review = new PlannerReview
        {
            Verdict = PlannerVerdict.PauseForReview,
            Reason = reviewReason,
            DecisionStatus = ReviewDecisionStatus.PendingReview,
            AcceptanceMet = true,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorktreePath = candidate.Lease.WorkspacePath,
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence =
                [
                    candidate.PathPolicy.Summary,
                    $"changed_paths={string.Join(",", candidate.ChangedPaths)}",
                ],
            },
            Patch = new PatchSummary(candidate.ChangedPaths.Count, 0, 0, true, candidate.ChangedPaths),
            WorkerExecution = workerResult,
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };
        var transition = new TaskTransitionDecision(
            DomainTaskStatus.Review,
            IncrementRetry: false,
            Reason: "Managed workspace submission reached the review boundary.");
        var reviewArtifact = reviewArtifactFactory.Create(task, report, review, transition);
        artifactRepository.SavePlannerReviewArtifact(reviewArtifact);

        task.RecordWorkerOutcome(workerResult);
        task = ApplyReviewDecision(task, DomainTaskStatus.Review, review);
        taskGraphService.ReplaceTask(task);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());

        var lines = new List<string>
        {
            $"Submitted managed workspace result for {task.TaskId}; task is now REVIEW.",
            $"Lease: {candidate.Lease.LeaseId}",
            $"Workspace: {candidate.Lease.WorkspacePath}",
            $"Changed files: {candidate.ChangedPaths.Count}",
        };
        lines.AddRange(candidate.ChangedPaths.Select(path => $"- {path}"));
        lines.Add("Next: review approve " + task.TaskId + " <reason...>");
        return new OperatorCommandResult(0, lines);
    }
}
