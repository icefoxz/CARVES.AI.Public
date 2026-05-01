using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using TaskGraphModel = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Platform;

public sealed partial class WorkbenchSurfaceService
{
    private IReadOnlyList<WorkbenchArtifactReference> BuildArtifacts(TaskNode task, ExecutionRun? latestRun)
    {
        var items = new List<WorkbenchArtifactReference>();
        AddArtifact(items, "worker_execution", task.LastWorkerDetailRef);
        AddArtifact(items, "provider_detail", task.LastProviderDetailRef);
        AddArtifact(items, "review_artifact", Path.Combine(".ai", "artifacts", "reviews", $"{task.TaskId}.json"));
        if (latestRun is not null)
        {
            AddArtifact(items, "result_envelope", latestRun.ResultEnvelopePath);
            AddArtifact(items, "boundary_violation", latestRun.BoundaryViolationPath);
            AddArtifact(items, "replan_request", latestRun.ReplanArtifactPath);
        }

        return items;
    }

    private static void AddArtifact(ICollection<WorkbenchArtifactReference> items, string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        items.Add(new WorkbenchArtifactReference
        {
            Label = label,
            Path = path,
        });
    }

    private WorkbenchReviewQueueItem ToReviewQueueItem(TaskNode task, IReadOnlyList<WorkbenchActionDescriptor> actions)
    {
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
        return new WorkbenchReviewQueueItem
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            Title = task.Title,
            Status = task.Status.ToString(),
            Summary = task.LastWorkerSummary ?? task.PlannerReview.Reason ?? "(none)",
            Reality = reviewArtifact is null
                ? new WorkbenchRealityReadModel()
                : BuildRealityProjection(reviewArtifact.RealityProjection),
            ReviewEvidence = BuildReviewEvidence(task, reviewArtifact, workerArtifact),
            AvailableActions = actions,
        };
    }

    private WorkbenchTaskListItem ToTaskListItem(TaskNode task, IReadOnlySet<string> completedTaskIds, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        var unresolvedDependencies = task.Dependencies.Where(dependency => !completedTaskIds.Contains(dependency)).ToArray();
        var draft = string.IsNullOrWhiteSpace(task.CardId) ? null : planningDraftService.TryGetCardDraft(task.CardId!);
        return new WorkbenchTaskListItem
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Status = task.Status.ToString(),
            Summary = task.LastWorkerSummary ?? task.PlannerReview.Reason ?? "(none)",
            Reality = ResolveTaskReality(task, draft),
            NextAction = ResolveTaskNextAction(task, unresolvedDependencies, formalPlanningExecutionGateService),
            BlockedReason = ResolveTaskBlockedReason(task, unresolvedDependencies, formalPlanningExecutionGateService),
        };
    }

    private WorkbenchRealityReadModel ResolveTaskReality(TaskNode task, CardDraftRecord? draft)
    {
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        if (reviewArtifact is not null)
        {
            return BuildRealityProjection(reviewArtifact.RealityProjection);
        }

        if (draft?.RealityModel is not null)
        {
            return new WorkbenchRealityReadModel
            {
                Status = ToSnakeCase(draft.RealityModel.SolidityClass),
                Summary = $"planned={draft.RealityModel.CurrentSolidScope}; next={draft.RealityModel.NextRealSlice}; proof_target={ToSnakeCase(draft.RealityModel.ProofTarget.Kind)}",
            };
        }

        return new WorkbenchRealityReadModel
        {
            Status = "ghost",
            Summary = "No reality projection recorded.",
        };
    }

    private WorkbenchRealityReadModel ResolveCardReality(string _, IReadOnlyList<TaskNode> tasks, CardDraftRecord? draft)
    {
        var projections = tasks
            .Select(task => new
            {
                Task = task,
                Artifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId),
            })
            .Where(item => item.Artifact is not null)
            .OrderByDescending(item => item.Task.UpdatedAt)
            .ToArray();
        var latestProjection = projections.FirstOrDefault()?.Artifact?.RealityProjection;
        var bestStatus = latestProjection?.SolidityClass
            ?? draft?.RealityModel?.SolidityClass
            ?? SolidityClass.Ghost;
        if (latestProjection is not null)
        {
            return new WorkbenchRealityReadModel
            {
                Status = ToSnakeCase(bestStatus),
                Summary = $"planned={latestProjection.PlannedScope}; verified={latestProjection.VerifiedOutcome}; promotion={latestProjection.PromotionResult}",
            };
        }

        if (draft?.RealityModel is not null)
        {
            return new WorkbenchRealityReadModel
            {
                Status = ToSnakeCase(bestStatus),
                Summary = $"planned={draft.RealityModel.CurrentSolidScope}; next={draft.RealityModel.NextRealSlice}; proof_target={ToSnakeCase(draft.RealityModel.ProofTarget.Kind)}",
            };
        }

        return new WorkbenchRealityReadModel
        {
            Status = "ghost",
            Summary = "No reality projection recorded.",
        };
    }

    private static WorkbenchRealityReadModel BuildRealityProjection(ReviewRealityProjection projection)
    {
        var proofTarget = projection.ProofTarget is null
            ? "proof_target=(none)"
            : $"proof_target={ToSnakeCase(projection.ProofTarget.Kind)}";
        return new WorkbenchRealityReadModel
        {
            Status = ToSnakeCase(projection.SolidityClass),
            Summary = $"planned={projection.PlannedScope}; verified={projection.VerifiedOutcome}; promotion={projection.PromotionResult}; {proofTarget}",
        };
    }

    private WorkbenchReviewEvidenceReadModel BuildReviewEvidence(
        TaskNode task,
        PlannerReviewArtifact? reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        var projection = reviewEvidenceProjectionService.Build(task, reviewArtifact, workerArtifact);
        return new WorkbenchReviewEvidenceReadModel
        {
            Status = projection.Status,
            CanFinalApprove = projection.CanFinalApprove,
            ClosureStatus = projection.ClosureStatus,
            ClosureWritebackAllowed = projection.ClosureWritebackAllowed,
            Summary = projection.Summary,
            MissingEvidence = projection.MissingAfterWriteback
                .Select(static gap => gap.DisplayLabel)
                .Concat(projection.ClosureBlockers.Select(static blocker => $"closure:{blocker}"))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ClosureBlockers = projection.ClosureBlockers,
            CompletionClaimStatus = projection.CompletionClaimStatus,
            CompletionClaimRequired = projection.CompletionClaimRequired,
            CompletionClaimSummary = projection.CompletionClaimSummary,
            CompletionClaimMissingFields = projection.CompletionClaimMissingFields,
            CompletionClaimEvidencePaths = projection.CompletionClaimEvidencePaths,
            HostValidationStatus = projection.HostValidationStatus,
            HostValidationRequired = projection.HostValidationRequired,
            HostValidationSummary = projection.HostValidationSummary,
            HostValidationBlockers = projection.HostValidationBlockers,
        };
    }

    private TaskNode? ResolveCurrentTask(TaskGraphModel graph, DispatchProjection dispatch, Carves.Runtime.Domain.Runtime.RuntimeSessionState? session)
    {
        if (!string.IsNullOrWhiteSpace(session?.CurrentTaskId))
        {
            return graph.Tasks.TryGetValue(session.CurrentTaskId, out var currentTask) ? currentTask : null;
        }

        if (!string.IsNullOrWhiteSpace(dispatch.NextTaskId))
        {
            return graph.Tasks.TryGetValue(dispatch.NextTaskId, out var nextTask) ? nextTask : null;
        }

        if (!string.IsNullOrWhiteSpace(session?.LastReviewTaskId))
        {
            return graph.Tasks.TryGetValue(session.LastReviewTaskId, out var reviewTask) ? reviewTask : null;
        }

        return graph.ListTasks().FirstOrDefault(task => task.Status == DomainTaskStatus.Running);
    }
}
