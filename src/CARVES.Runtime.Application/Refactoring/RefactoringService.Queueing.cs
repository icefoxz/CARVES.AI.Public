using System.Text.Json;
using System.Globalization;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Refactoring;

public sealed partial class RefactoringService
{
    private static readonly JsonSerializerOptions QueueJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly IReadOnlyList<RefactoringQueueDefinition> QueueDefinitions =
    [
        new RefactoringQueueDefinition(
            "host_bootstrap_dispatch_and_composition",
            "Host bootstrap, dispatch, and composition hotspot queue",
            "T-CARD-429-002",
            [
                "src/CARVES.Runtime.Host/",
            ],
            "Host bootstrap, dispatch, and composition accumulation points are decomposed through bounded routing and composition surfaces without reopening control-plane ownership.",
            [
                "keep the resident-host command surface and current truth ownership stable",
                "do not introduce a second host shell, second control plane, or unmanaged bootstrap lane",
            ],
            [
                "tests/Carves.Runtime.IntegrationTests/HostContractTests.cs",
                "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
            ]),
        new RefactoringQueueDefinition(
            "operator_projection_and_control_plane",
            "Operator projection and control-plane hotspot queue",
            "T-CARD-429-003",
            [
                "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter",
                "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService",
                "src/CARVES.Runtime.Application/ControlPlane/OperatorTaskFormatter.cs",
            ],
            "Operator projection and control-plane accumulation points are reduced without widening control-plane writes or collapsing projection/read boundaries.",
            [
                "preserve projection-versus-writeback separation for operator surfaces",
                "keep scope bounded to the declared control-plane family and coupled formatter/service slices",
            ],
            [
                "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
                "tests/Carves.Runtime.IntegrationTests/HostContractTests.cs",
            ]),
        new RefactoringQueueDefinition(
            "execution_review_and_boundary_pipeline",
            "Execution, review, and boundary pipeline hotspot queue",
            "T-CARD-429-004",
            [
                "src/CARVES.Runtime.Application/ControlPlane/",
            ],
            "Execution, review, and boundary accumulation points are decomposed into explicit services without reopening review/writeback ordering or packet/boundary truth.",
            [
                "preserve Verification -> Boundary Gate -> Truth Writeback ordering",
                "do not turn cleanup or packet handling into a second execution pipeline",
            ],
            [
                "tests/Carves.Runtime.Application.Tests/WorkerGovernanceTests.cs",
                "tests/Carves.Runtime.IntegrationTests/ValidationSurfaceTests.cs",
            ]),
        new RefactoringQueueDefinition(
            "context_and_worker_pipeline",
            "Context and worker pipeline hotspot queue",
            "T-CARD-429-005",
            [
                "src/CARVES.Runtime.Application/AI/",
                "src/CARVES.Runtime.Application/Workers/",
                "src/CARVES.Runtime.Infrastructure/AI/",
            ],
            "Context-pack, worker request, and adapter accumulation points are reduced without replacing provider-neutral worker boundaries or widening worker execution authority.",
            [
                "keep provider-neutral worker boundaries explicit",
                "do not bypass worker selection, safety, or approval orchestration",
            ],
            [
                "tests/Carves.Runtime.Application.Tests/ContextPackServiceTests.cs",
                "tests/Carves.Runtime.Application.Tests/CodexCliWorkerAdapterTests.cs",
                "tests/Carves.Runtime.Application.Tests/RemoteApiWorkerAdapterTests.cs",
            ]),
        new RefactoringQueueDefinition(
            "code_understanding_and_artifact_governance",
            "Code-understanding and artifact-governance hotspot queue",
            "T-CARD-429-006",
            [
                "src/CARVES.Runtime.Application/Platform/",
                "src/CARVES.Runtime.Application/CodeGraph/",
                "src/CARVES.Runtime.Infrastructure/CodeGraph/",
            ],
            "Code-understanding and artifact-governance accumulation points are decomposed while preserving .ai/codegraph-first truth and artifact policy boundaries.",
            [
                "do not replace .ai/codegraph with a second code-understanding store",
                "keep artifact policy and export/profile governance projection-only where required",
            ],
            [
                "tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs",
                "tests/Carves.Runtime.Application.Tests/RuntimeCodeUnderstandingEngineServiceTests.cs",
            ]),
    ];

    private static readonly string[] ExecutionBoundaryFileMarkers =
    [
        "Execution",
        "Review",
        "Boundary",
        "Packet",
        "ResultIngestion",
        "DispatchProjection",
        "ExecutionEnvelope",
    ];

    private RefactoringHotspotQueueSnapshot BuildQueueSnapshot(RefactoringBacklogSnapshot snapshot, Domain.Tasks.TaskGraph graph)
    {
        var activeItems = snapshot.Items
            .Where(item => item.Status is RefactoringBacklogStatus.Open or RefactoringBacklogStatus.Suggested)
            .OrderBy(item => PriorityWeight(item.Priority))
            .ThenByDescending(ItemMagnitudeScore)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingSnapshot = LoadQueueSnapshot();
        var existingQueues = existingSnapshot?.Queues.ToDictionary(queue => queue.QueueId, StringComparer.Ordinal)
            ?? new Dictionary<string, RefactoringHotspotQueue>(StringComparer.Ordinal);
        var queues = new List<RefactoringHotspotQueue>();

        foreach (var definition in QueueDefinitions)
        {
            var selectedItems = activeItems
                .Where(item => MatchesQueue(definition, item.Path))
                .Take(4)
                .ToArray();
            if (selectedItems.Length == 0)
            {
                continue;
            }

            existingQueues.TryGetValue(definition.QueueId, out var existingQueue);
            var queuePassContext = ResolveQueuePass(definition.QueueId, existingQueue, selectedItems, graph);

            queues.Add(new RefactoringHotspotQueue
            {
                QueueId = definition.QueueId,
                FamilyId = definition.QueueId,
                QueuePass = queuePassContext.QueuePass,
                Title = queuePassContext.QueuePass <= 1
                    ? definition.Title
                    : $"{definition.Title} (pass {queuePassContext.QueuePass})",
                Summary = BuildQueueSummary(definition.Title, queuePassContext.QueuePass),
                PlanningTaskId = definition.PlanningTaskId,
                SuggestedTaskId = queuePassContext.SuggestedTaskId,
                PreviousSuggestedTaskId = queuePassContext.PreviousSuggestedTaskId,
                ProofTarget = definition.ProofTarget,
                ScopeRoots = definition.ScopeRoots,
                PreservationConstraints = definition.PreservationConstraints,
                ValidationSurface = definition.ValidationSurface,
                BacklogItemIds = selectedItems.Select(item => item.ItemId).ToArray(),
                HotspotPaths = selectedItems.Select(item => item.Path).ToArray(),
            });
        }

        return new RefactoringHotspotQueueSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Queues = queues,
        };
    }

    private RefactoringHotspotQueueSnapshot? LoadQueueSnapshot()
    {
        var queueIndexPath = Path.Combine(repoRoot, ".ai", "refactoring", "queues", "index.json");
        if (!File.Exists(queueIndexPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RefactoringHotspotQueueSnapshot>(File.ReadAllText(queueIndexPath), QueueJsonOptions);
    }

    private void PersistQueueSnapshot(RefactoringHotspotQueueSnapshot snapshot)
    {
        var queueRoot = Path.Combine(repoRoot, ".ai", "refactoring", "queues");
        Directory.CreateDirectory(queueRoot);
        File.WriteAllText(
            Path.Combine(queueRoot, "index.json"),
            JsonSerializer.Serialize(snapshot, QueueJsonOptions));

        foreach (var queue in snapshot.Queues)
        {
            File.WriteAllText(
                Path.Combine(queueRoot, $"{queue.QueueId}.json"),
                JsonSerializer.Serialize(queue, QueueJsonOptions));
        }
    }

    private static TaskNode BuildSuggestedTask(RefactoringHotspotQueue queue, string baseCommit)
    {
        const TaskType taskType = TaskType.Execution;
        if (!TaskTypePolicy.AllowPlannerGeneration(taskType))
        {
            throw new InvalidOperationException("Refactoring backlog materialization must produce planner-generatable task types.");
        }

        return new TaskNode
        {
            TaskId = queue.SuggestedTaskId ?? BuildStableId("T-REFQ", queue.QueueId, queue.QueueId),
            Title = queue.Title,
            Description = queue.ProofTarget,
            Status = DomainTaskStatus.Suggested,
            TaskType = taskType,
            Priority = "P2",
            Source = "REFACTORING_BACKLOG",
            ProposalSource = TaskProposalSource.RefactoringBacklog,
            ProposalReason = queue.Summary,
            ProposalConfidence = 0.8,
            ProposalPriorityHint = "P2",
            CardId = null,
            BaseCommit = baseCommit,
            Scope = queue.ScopeRoots
                .Concat(queue.HotspotPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Acceptance =
            [
                "the selected hotspot backlog items are reduced or resolved without reappearing as the same queue inputs",
                "the patch stays bounded to the declared scope roots and selected hotspot paths",
                $"the queue's proof target is met: {queue.ProofTarget}",
            ],
            Constraints =
            [
                "do not auto-promote backlog cleanup into unbounded maintenance work",
                .. queue.PreservationConstraints,
            ],
            AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                queue.SuggestedTaskId ?? BuildStableId("T-REFQ", queue.QueueId, queue.QueueId),
                queue.Title,
                queue.ProofTarget,
                cardId: null,
                [
                    "the selected hotspot backlog items are reduced or resolved without reappearing as the same queue inputs",
                    "the patch stays bounded to the declared scope roots and selected hotspot paths",
                    $"the queue's proof target is met: {queue.ProofTarget}",
                ],
                [
                    "do not auto-promote backlog cleanup into unbounded maintenance work",
                    .. queue.PreservationConstraints,
                ],
                new ValidationPlan
                {
                    Checks =
                    [
                        "backlog detection reflects reduced queue pressure for the selected hotspot items",
                        "bounded validation surfaces remain green for the queue family",
                    ],
                    ExpectedEvidence =
                    [
                        "updated refactoring backlog",
                        $"queue snapshot .ai/refactoring/queues/{queue.QueueId}.json",
                        .. queue.ValidationSurface,
                    ],
                }),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["refactoring_queue_id"] = queue.QueueId,
                ["refactoring_family"] = queue.FamilyId,
                ["refactoring_queue_pass"] = queue.QueuePass.ToString(CultureInfo.InvariantCulture),
                ["proof_target"] = queue.ProofTarget,
                ["planning_anchor_task_id"] = queue.PlanningTaskId,
                ["backlog_item_ids"] = string.Join(",", queue.BacklogItemIds),
                ["hotspot_paths"] = string.Join(",", queue.HotspotPaths),
                ["validation_surface"] = string.Join(" | ", queue.ValidationSurface),
            },
            Validation = new ValidationPlan
            {
                Checks =
                [
                    "backlog detection reflects reduced queue pressure for the selected hotspot items",
                    "bounded validation surfaces remain green for the queue family",
                ],
                ExpectedEvidence =
                [
                    "updated refactoring backlog",
                    $"queue snapshot .ai/refactoring/queues/{queue.QueueId}.json",
                    .. queue.ValidationSurface,
                ],
            },
        };
    }

    private static QueuePassContext ResolveQueuePass(
        string queueId,
        RefactoringHotspotQueue? existingQueue,
        IReadOnlyList<RefactoringBacklogItem> selectedItems,
        Domain.Tasks.TaskGraph graph)
    {
        var baseTaskId = BuildStableId("T-REFQ", queueId, queueId);
        var currentTaskId = existingQueue?.SuggestedTaskId;
        if (string.IsNullOrWhiteSpace(currentTaskId))
        {
            currentTaskId = selectedItems
                .Select(item => item.SuggestedTaskId)
                .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
                .GroupBy(taskId => taskId!, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key)
                .FirstOrDefault()
                ?? baseTaskId;
        }

        var currentPass = existingQueue?.QueuePass > 0
            ? existingQueue.QueuePass
            : ParseQueuePass(currentTaskId, baseTaskId);
        var previousTaskId = existingQueue?.PreviousSuggestedTaskId;

        graph.Tasks.TryGetValue(currentTaskId, out var currentTask);
        if (currentTask?.Status == DomainTaskStatus.Completed)
        {
            var nextPass = currentPass + 1;
            return new QueuePassContext(BuildQueuePassTaskId(baseTaskId, nextPass), nextPass, currentTaskId);
        }

        return new QueuePassContext(currentTaskId, currentPass, previousTaskId);
    }

    private static string BuildQueueSummary(string title, int queuePass)
    {
        return queuePass <= 1
            ? $"First bounded hotspot-clearance queue for {title.ToLowerInvariant()}."
            : $"Pass {queuePass.ToString(CultureInfo.InvariantCulture)} bounded hotspot-clearance queue for {title.ToLowerInvariant()}.";
    }

    private static string BuildQueuePassTaskId(string baseTaskId, int queuePass)
    {
        return queuePass <= 1
            ? baseTaskId
            : $"{baseTaskId}-P{queuePass.ToString("000", CultureInfo.InvariantCulture)}";
    }

    private static int ParseQueuePass(string taskId, string baseTaskId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.Equals(taskId, baseTaskId, StringComparison.Ordinal))
        {
            return 1;
        }

        var prefix = $"{baseTaskId}-P";
        if (!taskId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 1;
        }

        var passToken = taskId[prefix.Length..];
        return int.TryParse(passToken, NumberStyles.None, CultureInfo.InvariantCulture, out var queuePass) && queuePass > 1
            ? queuePass
            : 1;
    }

    private static bool MatchesQueue(RefactoringQueueDefinition definition, string path)
    {
        if (string.Equals(definition.QueueId, "execution_review_and_boundary_pipeline", StringComparison.Ordinal))
        {
            return path.StartsWith("src/CARVES.Runtime.Application/ControlPlane/", StringComparison.Ordinal)
                   && ExecutionBoundaryFileMarkers.Any(marker => path.Contains(marker, StringComparison.Ordinal));
        }

        return definition.ScopeRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal));
    }

    private static int ItemMagnitudeScore(RefactoringBacklogItem item)
    {
        return item.Metrics.Values.DefaultIfEmpty(0).Max();
    }

    private sealed record RefactoringQueueDefinition(
        string QueueId,
        string Title,
        string PlanningTaskId,
        IReadOnlyList<string> ScopeRoots,
        string ProofTarget,
        IReadOnlyList<string> PreservationConstraints,
        IReadOnlyList<string> ValidationSurface);

    private sealed record QueuePassContext(string SuggestedTaskId, int QueuePass, string? PreviousSuggestedTaskId);
}
