using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeHotspotBacklogDrainService
{
    private static readonly JsonSerializerOptions QueueJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly IRefactoringService refactoringService;
    private readonly TaskGraphService taskGraphService;

    public RuntimeHotspotBacklogDrainService(
        string repoRoot,
        ControlPlanePaths paths,
        IRefactoringService refactoringService,
        TaskGraphService taskGraphService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.refactoringService = refactoringService;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeHotspotBacklogDrainSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var boundaryDocumentPath = "docs/runtime/runtime-hotspot-backlog-drain-governance.md";
        if (!File.Exists(Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar))))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var queueIndexPath = ".ai/refactoring/queues/index.json";
        var queueSnapshot = LoadQueueSnapshot(Path.Combine(repoRoot, queueIndexPath.Replace('/', Path.DirectorySeparatorChar)));
        if (queueSnapshot is null)
        {
            errors.Add($"Queue snapshot '{queueIndexPath}' is missing or invalid.");
            queueSnapshot = new RefactoringHotspotQueueSnapshot();
        }

        var backlog = refactoringService.LoadBacklog();
        var continuationGatePolicyService = new RuntimeGovernanceContinuationGatePolicyService(paths);
        var continuationGatePolicy = continuationGatePolicyService.LoadPolicy();
        var continuationGateValidation = continuationGatePolicyService.Validate();
        errors.AddRange(continuationGateValidation.Errors);
        warnings.AddRange(continuationGateValidation.Warnings);

        if (backlog.Items.Count == 0)
        {
            warnings.Add("Refactoring backlog is empty; sustained drain surface has no current backlog findings to project.");
        }

        if (queueSnapshot.Queues.Count == 0)
        {
            warnings.Add("Governed hotspot queue snapshot is empty; no bounded queue families are currently projected.");
        }

        var graph = taskGraphService.Load();
        var backlogById = backlog.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var activeBacklog = backlog.Items
            .Where(IsActiveBacklogItem)
            .ToArray();
        var queues = queueSnapshot.Queues
            .Select(queue => BuildQueueSurface(queue, graph, backlogById, continuationGatePolicy))
            .ToArray();
        var selectedActiveBacklogIds = queues
            .SelectMany(queue => queue.SelectedBacklogItemIds)
            .Where(backlogById.ContainsKey)
            .Where(itemId => backlogById[itemId].Status is RefactoringBacklogStatus.Open or RefactoringBacklogStatus.Suggested)
            .ToHashSet(StringComparer.Ordinal);
        var unselectedActiveBacklog = activeBacklog
            .Where(item => !selectedActiveBacklogIds.Contains(item.ItemId))
            .ToArray();
        var unselectedClosureRelevantBacklogCount = unselectedActiveBacklog.Count(item => IsUnselectedClosureRelevant(item, continuationGatePolicy));
        var unselectedMaintenanceNoiseBacklogCount = unselectedActiveBacklog.Length - unselectedClosureRelevantBacklogCount;
        var residualQueues = queues
            .Where(queue => string.Equals(queue.ClosureState, "residual_open", StringComparison.Ordinal))
            .ToArray();

        return new RuntimeHotspotBacklogDrainSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            BacklogPath = ToRepoRelative(Path.Combine(paths.AiRoot, "refactoring", "backlog.json")),
            QueueIndexPath = queueIndexPath,
            Summary = "Sustained hotspot and refactoring backlog drain remains a queue-governed, read-only Runtime projection over backlog detection truth, queue snapshots, and governed task lifecycle truth.",
            QueueSnapshotGeneratedAt = queueSnapshot.GeneratedAt,
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Counts = new RuntimeHotspotBacklogDrainCountsSurface
            {
                TotalBacklogItems = backlog.Items.Count,
                OpenBacklogItems = backlog.Items.Count(item => item.Status == RefactoringBacklogStatus.Open),
                SuggestedBacklogItems = backlog.Items.Count(item => item.Status == RefactoringBacklogStatus.Suggested),
                ResolvedBacklogItems = backlog.Items.Count(item => item.Status == RefactoringBacklogStatus.Resolved),
                SuppressedBacklogItems = backlog.Items.Count(item => item.Status == RefactoringBacklogStatus.Suppressed),
                QueueFamilyCount = queues.Length,
                MaterializedTaskCount = queues.Count(queue => !string.Equals(queue.SuggestedTaskStatus, "none", StringComparison.Ordinal)),
                CompletedQueueCount = queues.Count(queue => string.Equals(queue.DrainState, "completed_for_selected_items", StringComparison.Ordinal)),
                AcceptedResidualQueueCount = queues.Count(queue => string.Equals(queue.ClosureState, "accepted_residual_concentration", StringComparison.Ordinal)),
                GovernedCompletedQueueCount = queues.Count(queue =>
                    string.Equals(queue.DrainState, "completed_for_selected_items", StringComparison.Ordinal)
                    || string.Equals(queue.DrainState, "completed_with_remaining_backlog", StringComparison.Ordinal)
                    || (queue.QueuePass > 1 && string.Equals(queue.PreviousSuggestedTaskStatus, "completed", StringComparison.Ordinal))),
                CompletedWithRemainingBacklogCount = queues.Count(queue =>
                    string.Equals(queue.ClosureState, "residual_open", StringComparison.Ordinal)
                    && (string.Equals(queue.DrainState, "completed_with_remaining_backlog", StringComparison.Ordinal)
                        || (queue.QueuePass > 1
                            && string.Equals(queue.PreviousSuggestedTaskStatus, "completed", StringComparison.Ordinal)
                            && (queue.OpenBacklogItemCount > 0 || queue.SuggestedBacklogItemCount > 0)))),
                ResidualOpenQueueCount = queues.Count(queue => string.Equals(queue.ClosureState, "residual_open", StringComparison.Ordinal)),
                ContinuedQueueCount = queues.Count(queue => queue.QueuePass > 1),
                ClosureBlockingBacklogItemCount = residualQueues.Sum(queue => queue.ClosureBlockingBacklogItemCount),
                NonBlockingBacklogItemCount = queues.Sum(queue => queue.NonBlockingBacklogItemCount),
                UnselectedBacklogItemCount = unselectedActiveBacklog.Length,
                UnselectedClosureRelevantBacklogItemCount = unselectedClosureRelevantBacklogCount,
                UnselectedMaintenanceNoiseBacklogItemCount = unselectedMaintenanceNoiseBacklogCount,
            },
            Queues = queues,
            NonClaims =
            [
                "Backlog detection truth does not become a second execution queue.",
                "Queue materialization does not by itself prove execution completion.",
                "One completed queue task does not mean the entire hotspot family is globally cleared.",
                "Accepted residual concentration only applies when repo-local gate policy says the remaining backlog is non-blocking.",
                "Active backlog residue outside the current selected residual program remains visible and is not silently treated as resolved."
            ],
        };
    }

    private static RuntimeHotspotBacklogDrainQueueSurface BuildQueueSurface(
        RefactoringHotspotQueue queue,
        Domain.Tasks.TaskGraph graph,
        IReadOnlyDictionary<string, RefactoringBacklogItem> backlogById,
        GovernanceContinuationGateRuntimePolicy continuationGatePolicy)
    {
        graph.Tasks.TryGetValue(queue.SuggestedTaskId ?? string.Empty, out var materializedTask);
        graph.Tasks.TryGetValue(queue.PreviousSuggestedTaskId ?? string.Empty, out var previousTask);
        var selectedItems = queue.BacklogItemIds
            .Select(itemId => backlogById.TryGetValue(itemId, out var item) ? item : null)
            .Where(item => item is not null)
            .Cast<RefactoringBacklogItem>()
            .ToArray();

        var openCount = selectedItems.Count(item => item.Status == RefactoringBacklogStatus.Open);
        var suggestedCount = selectedItems.Count(item => item.Status == RefactoringBacklogStatus.Suggested);
        var resolvedCount = selectedItems.Count(item => item.Status == RefactoringBacklogStatus.Resolved);
        var closureBlockingCount = selectedItems.Count(item => IsActiveBacklogItem(item) && IsClosureBlocking(item, continuationGatePolicy));
        var nonBlockingCount = selectedItems.Count(item => IsActiveBacklogItem(item) && !IsClosureBlocking(item, continuationGatePolicy));
        var residualRelevantOpenCount = selectedItems.Count(item => item.Status == RefactoringBacklogStatus.Open && !IsSelectedMaintenanceNoise(item, continuationGatePolicy));
        var residualRelevantSuggestedCount = selectedItems.Count(item => item.Status == RefactoringBacklogStatus.Suggested && !IsSelectedMaintenanceNoise(item, continuationGatePolicy));
        var drainState = ClassifyDrainState(queue.QueuePass, materializedTask, previousTask, residualRelevantOpenCount, residualRelevantSuggestedCount, resolvedCount);
        var closureState = ClassifyClosureState(queue, materializedTask, previousTask, drainState, closureBlockingCount, continuationGatePolicy);

        return new RuntimeHotspotBacklogDrainQueueSurface
        {
            QueueId = queue.QueueId,
            FamilyId = queue.FamilyId,
            QueuePass = queue.QueuePass,
            Title = queue.Title,
            Summary = queue.Summary,
            PlanningTaskId = queue.PlanningTaskId,
            SuggestedTaskId = queue.SuggestedTaskId,
            PreviousSuggestedTaskId = queue.PreviousSuggestedTaskId,
            SuggestedTaskStatus = materializedTask is null ? "none" : materializedTask.Status.ToString().ToLowerInvariant(),
            PreviousSuggestedTaskStatus = previousTask is null ? "none" : previousTask.Status.ToString().ToLowerInvariant(),
            DrainState = drainState,
            ClosureState = closureState,
            ProofTarget = queue.ProofTarget,
            SelectedBacklogItemCount = queue.BacklogItemIds.Count,
            OpenBacklogItemCount = openCount,
            SuggestedBacklogItemCount = suggestedCount,
            ResolvedBacklogItemCount = resolvedCount,
            ClosureBlockingBacklogItemCount = closureBlockingCount,
            NonBlockingBacklogItemCount = nonBlockingCount,
            SelectedBacklogItemIds = queue.BacklogItemIds,
            ScopeRoots = queue.ScopeRoots,
            HotspotPaths = queue.HotspotPaths,
            ValidationSurface = queue.ValidationSurface,
            PreservationConstraints = queue.PreservationConstraints,
            NonClaims =
            [
                "Queue family presence does not auto-dispatch worker execution.",
                "Queue family completion remains bounded to the selected backlog items and governed task truth.",
                "Closure-state projection does not synthesize queue clearance when residual backlog is still closure-blocking."
            ],
        };
    }

    private static string ClassifyDrainState(int queuePass, TaskNode? materializedTask, TaskNode? previousTask, int openCount, int suggestedCount, int resolvedCount)
    {
        if (materializedTask is null)
        {
            return resolvedCount > 0 && openCount == 0 && suggestedCount == 0
                ? "selection_resolved_without_materialized_task"
                : "backlog_detected";
        }

        return materializedTask.Status switch
        {
            DomainTaskStatus.Suggested when queuePass > 1 && previousTask?.Status == DomainTaskStatus.Completed => "continuation_materialized",
            DomainTaskStatus.Suggested => "materialized",
            DomainTaskStatus.Pending or DomainTaskStatus.Running or DomainTaskStatus.Testing or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait or DomainTaskStatus.Blocked
                when queuePass > 1 && previousTask?.Status == DomainTaskStatus.Completed => "continuation_governed_work_open",
            DomainTaskStatus.Pending or DomainTaskStatus.Running or DomainTaskStatus.Testing or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait or DomainTaskStatus.Blocked => "governed_work_open",
            DomainTaskStatus.Completed when openCount == 0 && suggestedCount == 0 => "completed_for_selected_items",
            DomainTaskStatus.Completed => "completed_with_remaining_backlog",
            _ => materializedTask.Status.ToString().ToLowerInvariant(),
        };
    }

    private static string ClassifyClosureState(
        RefactoringHotspotQueue queue,
        TaskNode? materializedTask,
        TaskNode? previousTask,
        string drainState,
        int closureBlockingCount,
        GovernanceContinuationGateRuntimePolicy continuationGatePolicy)
    {
        if (string.Equals(drainState, "completed_for_selected_items", StringComparison.Ordinal))
        {
            return "cleared";
        }

        var governedCompleted =
            string.Equals(drainState, "completed_with_remaining_backlog", StringComparison.Ordinal)
            || (queue.QueuePass > 1 && previousTask?.Status == DomainTaskStatus.Completed)
            || materializedTask?.Status == DomainTaskStatus.Completed;

        if (governedCompleted
            && continuationGatePolicy.AcceptedResidualConcentrationFamilies.Contains(queue.FamilyId, StringComparer.Ordinal)
            && closureBlockingCount == 0)
        {
            return "accepted_residual_concentration";
        }

        return "residual_open";
    }

    private static bool IsActiveBacklogItem(RefactoringBacklogItem item)
    {
        return item.Status is RefactoringBacklogStatus.Open or RefactoringBacklogStatus.Suggested;
    }

    private static bool IsClosureBlocking(RefactoringBacklogItem item, GovernanceContinuationGateRuntimePolicy continuationGatePolicy)
    {
        return continuationGatePolicy.ClosureBlockingBacklogKinds.Contains(item.Kind, StringComparer.Ordinal)
               && !string.Equals(item.Priority, "P3", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectedMaintenanceNoise(RefactoringBacklogItem item, GovernanceContinuationGateRuntimePolicy continuationGatePolicy)
    {
        return continuationGatePolicy.ClosureBlockingBacklogKinds.Contains(item.Kind, StringComparer.Ordinal)
               && string.Equals(item.Priority, "P3", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnselectedClosureRelevant(RefactoringBacklogItem item, GovernanceContinuationGateRuntimePolicy continuationGatePolicy)
    {
        return IsClosureBlocking(item, continuationGatePolicy);
    }

    private static RefactoringHotspotQueueSnapshot? LoadQueueSnapshot(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<RefactoringHotspotQueueSnapshot>(File.ReadAllText(path), QueueJsonOptions);
        return snapshot;
    }

    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
