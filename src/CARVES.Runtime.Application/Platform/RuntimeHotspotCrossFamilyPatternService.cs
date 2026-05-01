using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeHotspotCrossFamilyPatternService
{
    private static readonly BoundaryCategoryDefinition[] BoundaryCategoryDefinitions =
    [
        new(
            "truth_ownership_boundary",
            "Truth and ownership boundary",
            "Shared extraction work still clusters around truth ownership, control-plane ownership, and codegraph/artifact truth boundaries.",
            ["truth", "ownership", "control-plane ownership", "codegraph"]),
        new(
            "projection_read_boundary",
            "Projection and read boundary",
            "Shared extraction work still clusters around read-model, projection, and projection-only boundaries.",
            ["projection", "read-only", "projection-only", "read boundary"]),
        new(
            "verification_review_boundary",
            "Verification and review boundary",
            "Shared extraction work still clusters around verification, review, boundary, packet, and writeback ordering constraints.",
            ["verification", "review", "boundary", "packet", "writeback"]),
        new(
            "worker_authority_boundary",
            "Worker and authority boundary",
            "Shared extraction work still clusters around worker authority, provider-neutral routing, dispatch, and approval orchestration constraints.",
            ["worker", "provider", "dispatch", "routing", "approval orchestration", "execution authority"]),
    ];

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly IRefactoringService refactoringService;
    private readonly TaskGraphService taskGraphService;

    public RuntimeHotspotCrossFamilyPatternService(
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

    public RuntimeHotspotCrossFamilyPatternSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var boundaryDocumentPath = "docs/runtime/runtime-hotspot-cross-family-patterns.md";
        if (!File.Exists(Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar))))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var drain = new RuntimeHotspotBacklogDrainService(repoRoot, paths, refactoringService, taskGraphService).Build();
        errors.AddRange(drain.Errors.Select(error => $"Hotspot drain surface: {error}"));
        warnings.AddRange(drain.Warnings.Select(warning => $"Hotspot drain surface: {warning}"));

        var backlog = refactoringService.LoadBacklog();
        var backlogById = backlog.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var queues = drain.Queues
            .Where(queue => !string.IsNullOrWhiteSpace(queue.SuggestedTaskId))
            .ToArray();

        if (queues.Length == 0)
        {
            warnings.Add("No materialized hotspot queues are currently available for cross-family pattern extraction.");
        }

        var patterns = BuildPatterns(queues, backlogById);
        var boundaryCategories = BuildBoundaryCategories(queues);

        return new RuntimeHotspotCrossFamilyPatternSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            SourceBoundaryDocumentPath = drain.BoundaryDocumentPath,
            BacklogPath = drain.BacklogPath,
            QueueIndexPath = drain.QueueIndexPath,
            Summary = "Cross-family hotspot patterns remain a bounded, read-only Runtime projection over current hotspot drain truth, backlog truth, and governed task lifecycle truth.",
            QueueSnapshotGeneratedAt = drain.QueueSnapshotGeneratedAt,
            IsValid = errors.Count == 0 && drain.IsValid,
            Errors = errors,
            Warnings = warnings,
            Counts = new RuntimeHotspotCrossFamilyPatternCountsSurface
            {
                QueueFamilyCount = queues.Length,
                ContinuedQueueCount = queues.Count(IsHistoricalContinuationQueue),
                PatternCount = patterns.Length,
                ResidualPatternCount = patterns.Count(pattern => string.Equals(pattern.PatternType, "residual_continuation", StringComparison.Ordinal)),
                RepeatedBacklogKindPatternCount = patterns.Count(pattern => string.Equals(pattern.PatternType, "repeated_backlog_kind", StringComparison.Ordinal)),
                ValidationOverlapPatternCount = patterns.Count(pattern => string.Equals(pattern.PatternType, "validation_overlap", StringComparison.Ordinal)),
                BoundaryCategoryCount = boundaryCategories.Length,
                SharedBoundaryCategoryCount = boundaryCategories.Count(category => category.QueueCount > 1),
            },
            Patterns = patterns,
            BoundaryCategories = boundaryCategories,
            NonClaims =
            [
                "Cross-family patterns do not become a second refactoring queue.",
                "Cross-family patterns do not auto-promote suggested queue tasks into execution.",
                "Pattern detection does not claim the queue families are already cleared."
            ],
        };
    }

    private static RuntimeHotspotCrossFamilyPatternEntrySurface[] BuildPatterns(
        IReadOnlyList<RuntimeHotspotBacklogDrainQueueSurface> queues,
        IReadOnlyDictionary<string, RefactoringBacklogItem> backlogById)
    {
        var patterns = new List<RuntimeHotspotCrossFamilyPatternEntrySurface>();
        if (queues.Count == 0)
        {
            return [];
        }

        var residualQueues = queues.Where(IsActiveResidualContinuationQueue).ToArray();
        if (residualQueues.Length > 0)
        {
            patterns.Add(new RuntimeHotspotCrossFamilyPatternEntrySurface
            {
                PatternId = "residual_continuation_pressure",
                DisplayName = "Residual continuation pressure",
                PatternType = "residual_continuation",
                Summary = $"Current governed hotspot drain still projects {residualQueues.Length} continuation-oriented queue families with residual backlog pressure rather than cleared families.",
                QueueCount = residualQueues.Length,
                QueueIds = residualQueues.Select(queue => queue.QueueId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                SupportingTaskIds = residualQueues.Select(queue => queue.SuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                PreviousTaskIds = residualQueues.Select(queue => queue.PreviousSuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogItemIds = residualQueues.SelectMany(queue => queue.SelectedBacklogItemIds).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogKinds = residualQueues
                    .SelectMany(queue => queue.SelectedBacklogItemIds)
                    .Select(itemId => backlogById.TryGetValue(itemId, out var item) ? item.Kind : null)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                ValidationSurfaces = residualQueues.SelectMany(queue => queue.ValidationSurface).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                RepresentativePaths = residualQueues.SelectMany(queue => queue.HotspotPaths).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                NonClaims =
                [
                    "Continuation pressure does not imply another queue family should be invented.",
                    "Continuation pressure does not claim the current pass already implemented the remaining extraction work."
                ],
            });
        }

        var repeatedKinds = queues
            .Where(IsActiveResidualContinuationQueue)
            .SelectMany(queue => queue.SelectedBacklogItemIds.Select(itemId => (queue, itemId)))
            .Where(entry => backlogById.ContainsKey(entry.itemId))
            .Select(entry => (entry.queue, item: backlogById[entry.itemId]))
            .Where(entry => IsActiveBacklogItem(entry.item))
            .GroupBy(entry => entry.item.Kind, StringComparer.Ordinal)
            .Where(group => group.Count() >= 2)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in repeatedKinds)
        {
            var groupedEntries = group.ToArray();
            patterns.Add(new RuntimeHotspotCrossFamilyPatternEntrySurface
            {
                PatternId = $"backlog_kind_{group.Key}",
                DisplayName = $"Repeated backlog kind: {group.Key}",
                PatternType = "repeated_backlog_kind",
                Summary = $"Backlog kind '{group.Key}' currently recurs across {groupedEntries.Select(entry => entry.queue.QueueId).Distinct(StringComparer.Ordinal).Count()} queue families and {groupedEntries.Length} selected backlog items.",
                QueueCount = groupedEntries.Select(entry => entry.queue.QueueId).Distinct(StringComparer.Ordinal).Count(),
                QueueIds = groupedEntries.Select(entry => entry.queue.QueueId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                SupportingTaskIds = groupedEntries.Select(entry => entry.queue.SuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                PreviousTaskIds = groupedEntries.Select(entry => entry.queue.PreviousSuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogItemIds = groupedEntries.Select(entry => entry.item.ItemId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogKinds = [group.Key],
                ValidationSurfaces = groupedEntries.SelectMany(entry => entry.queue.ValidationSurface).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                RepresentativePaths = groupedEntries.Select(entry => entry.item.Path).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                NonClaims =
                [
                    "Repeated backlog kind does not auto-schedule a broad cleanup program.",
                    "Repeated backlog kind does not claim every file in the family shares one fix strategy."
                ],
            });
        }

        var repeatedValidationSurfaces = queues
            .SelectMany(queue => queue.ValidationSurface.Select(surface => (queue, surface)))
            .GroupBy(entry => entry.surface, StringComparer.Ordinal)
            .Where(group => group.Select(entry => entry.queue.QueueId).Distinct(StringComparer.Ordinal).Count() >= 2)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in repeatedValidationSurfaces)
        {
            var groupedEntries = group.ToArray();
            var queueIds = groupedEntries.Select(entry => entry.queue.QueueId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            patterns.Add(new RuntimeHotspotCrossFamilyPatternEntrySurface
            {
                PatternId = $"validation_overlap_{Path.GetFileNameWithoutExtension(group.Key).ToLowerInvariant()}",
                DisplayName = $"Validation overlap: {group.Key}",
                PatternType = "validation_overlap",
                Summary = $"Validation surface '{group.Key}' is shared by {queueIds.Length} queue families, making it a repeated cross-family proof dependency.",
                QueueCount = queueIds.Length,
                QueueIds = queueIds,
                SupportingTaskIds = groupedEntries.Select(entry => entry.queue.SuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                PreviousTaskIds = groupedEntries.Select(entry => entry.queue.PreviousSuggestedTaskId).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogItemIds = groupedEntries.SelectMany(entry => entry.queue.SelectedBacklogItemIds).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BacklogKinds = groupedEntries
                    .SelectMany(entry => entry.queue.SelectedBacklogItemIds)
                    .Select(itemId => backlogById.TryGetValue(itemId, out var item) ? item.Kind : null)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                ValidationSurfaces = [group.Key],
                RepresentativePaths = groupedEntries.SelectMany(entry => entry.queue.HotspotPaths).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                NonClaims =
                [
                    "Shared validation surface does not collapse the queue families into one execution task.",
                    "Validation overlap does not claim the test surface alone proves hotspot clearance."
                ],
            });
        }

        return patterns
            .OrderByDescending(pattern => pattern.QueueCount)
            .ThenBy(pattern => pattern.PatternType, StringComparer.Ordinal)
            .ThenBy(pattern => pattern.PatternId, StringComparer.Ordinal)
            .ToArray();
    }

    private static RuntimeHotspotCrossFamilyBoundaryCategorySurface[] BuildBoundaryCategories(
        IReadOnlyList<RuntimeHotspotBacklogDrainQueueSurface> queues)
    {
        return BoundaryCategoryDefinitions
            .Select(definition => BuildBoundaryCategory(definition, queues))
            .Where(category => category is not null)
            .Cast<RuntimeHotspotCrossFamilyBoundaryCategorySurface>()
            .OrderByDescending(category => category.QueueCount)
            .ThenBy(category => category.CategoryId, StringComparer.Ordinal)
            .ToArray();
    }

    private static RuntimeHotspotCrossFamilyBoundaryCategorySurface? BuildBoundaryCategory(
        BoundaryCategoryDefinition definition,
        IReadOnlyList<RuntimeHotspotBacklogDrainQueueSurface> queues)
    {
        var matches = queues
            .Select(queue => new
            {
                Queue = queue,
                Statements = BuildStatements(queue),
                Keywords = definition.Keywords
                    .Where(keyword => BuildStatements(queue).Any(statement => statement.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .Where(entry => entry.Keywords.Length > 0)
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        return new RuntimeHotspotCrossFamilyBoundaryCategorySurface
        {
            CategoryId = definition.CategoryId,
            DisplayName = definition.DisplayName,
            Summary = definition.Summary,
            QueueCount = matches.Length,
            QueueIds = matches.Select(entry => entry.Queue.QueueId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            MatchingKeywords = matches.SelectMany(entry => entry.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            RepresentativePaths = matches.SelectMany(entry => entry.Queue.HotspotPaths).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            SupportingStatements = matches
                .SelectMany(entry => entry.Statements.Where(statement => entry.Keywords.Any(keyword => statement.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
                .Distinct(StringComparer.Ordinal)
                .Take(10)
                .ToArray(),
            NonClaims =
            [
                "Boundary categories do not replace queue families as the execution unit.",
                "Boundary categories do not authorize a cross-family mega-refactor."
            ],
        };
    }

    private static bool IsActiveResidualContinuationQueue(RuntimeHotspotBacklogDrainQueueSurface queue)
    {
        return string.Equals(queue.ClosureState, "residual_open", StringComparison.Ordinal)
               || string.Equals(queue.DrainState, "completed_with_remaining_backlog", StringComparison.Ordinal)
               || queue.DrainState.StartsWith("continuation_", StringComparison.Ordinal);
    }

    private static bool IsHistoricalContinuationQueue(RuntimeHotspotBacklogDrainQueueSurface queue)
    {
        return queue.QueuePass > 1;
    }

    private static bool IsActiveBacklogItem(RefactoringBacklogItem item)
    {
        return item.Status is RefactoringBacklogStatus.Open or RefactoringBacklogStatus.Suggested;
    }

    private static string[] BuildStatements(RuntimeHotspotBacklogDrainQueueSurface queue)
    {
        return
        [
            queue.Summary,
            queue.ProofTarget,
            .. queue.PreservationConstraints,
            .. queue.NonClaims,
        ];
    }

    private sealed record BoundaryCategoryDefinition(
        string CategoryId,
        string DisplayName,
        string Summary,
        IReadOnlyList<string> Keywords);
}
