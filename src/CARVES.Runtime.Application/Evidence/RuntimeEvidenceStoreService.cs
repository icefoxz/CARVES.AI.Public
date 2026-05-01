using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Evidence;

public sealed class RuntimeEvidenceStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeSurfaceRouteGraphService runtimeSurfaceRouteGraphService;

    public RuntimeEvidenceStoreService(ControlPlanePaths paths)
    {
        this.paths = paths;
        runtimeSurfaceRouteGraphService = new RuntimeSurfaceRouteGraphService(paths);
    }

    public RuntimeEvidenceRecord RecordContextPack(
        ContextPack pack,
        string? cardId,
        string? sessionId,
        IReadOnlyList<string>? sourceEvidenceIds = null)
    {
        var summary = $"{pack.Audience} context pack built with profile {pack.Budget.ProfileId}, posture {pack.Budget.BudgetPosture}, {pack.RelevantModules.Count} modules, and {pack.Recall.Count} recall items.";
        var excerpt = PromptSafeArtifactProjectionFactory.Create(
            $"{summary} Facets: {pack.FacetNarrowing.Phase}. Recall: {string.Join(" | ", pack.Recall.Select(item => item.Text))}",
            $"{summary} Facets: {pack.FacetNarrowing.Phase}. Recall: {string.Join(" | ", pack.Recall.Select(item => item.Text))}",
            pack.ArtifactPath);
        var record = Append(
            RuntimeEvidenceKind.ContextPack,
            taskId: pack.TaskId,
            cardId: cardId,
            runId: pack.LastRunSummary?.RunId,
            sessionId: sessionId,
            producer: "ContextPackService",
            scope: pack.TaskId is null ? $"session:{sessionId ?? "planner"}" : $"task:{pack.TaskId}",
            summary: summary,
            excerpt: excerpt,
            artifactPaths: DistinctPaths(
                new[] { pack.ArtifactPath }
                    .Concat(pack.ExpandableReferences.Select(item => item.Path))
                    .Concat(pack.Recall.Select(item => item.Source))),
            sourceEvidenceIds: sourceEvidenceIds,
            lineage: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["audience"] = pack.Audience.ToString(),
                ["profile_id"] = pack.Budget.ProfileId,
                ["budget_posture"] = pack.Budget.BudgetPosture,
                ["facet_phase"] = pack.FacetNarrowing.Phase,
            });
        runtimeSurfaceRouteGraphService.RecordSurface(
            $"evidence:{record.EvidenceId}",
            "RuntimeEvidenceStoreService",
            "evidence_surface",
            $"{record.Summary}\n{record.Excerpt}");
        runtimeSurfaceRouteGraphService.RecordRouteEdge(new RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = $"evidence:{record.EvidenceId}",
            Consumer = "RuntimeEvidenceStoreService",
            DeclaredRouteKind = "persist_only",
            ObservedRouteKind = "persist_only",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            EvidenceSource = record.EvidenceId,
            LastSeen = record.RecordedAtUtc,
        });
        return record;
    }

    public RuntimeEvidenceRecord RecordExecutionRun(
        ExecutionRun run,
        ExecutionRunReport report,
        ResultEnvelope? resultEnvelope = null,
        FailureReport? failure = null,
        IReadOnlyList<string>? sourceEvidenceIds = null)
    {
        var summary = $"Execution run {run.RunId} finished as {run.Status} with {report.FilesChanged} files changed and {report.CompletedSteps}/{report.TotalSteps} completed steps.";
        var detail = $"{summary} Boundary={report.BoundaryReason?.ToString() ?? "none"}; failure={report.FailureType?.ToString() ?? "none"}; modules={string.Join(", ", report.ModulesTouched)}.";
        return Append(
            RuntimeEvidenceKind.ExecutionRun,
            taskId: run.TaskId,
            cardId: null,
            runId: run.RunId,
            sessionId: null,
            producer: nameof(ExecutionRunReportService),
            scope: $"task:{run.TaskId}",
            summary: summary,
            excerpt: PromptSafeArtifactProjectionFactory.Create(detail, detail, GetReportRepoRelativePath(run.TaskId, run.RunId)),
            artifactPaths: DistinctPaths(
                new[]
                {
                    GetReportRepoRelativePath(run.TaskId, run.RunId),
                    resultEnvelope?.ExecutionEvidencePath,
                    run.ResultEnvelopePath,
                    run.BoundaryViolationPath,
                    run.ReplanArtifactPath,
                    failure is null ? null : Path.Combine(".ai", "failures", $"{failure.Id}.json").Replace('\\', '/'),
                }),
            sourceEvidenceIds: sourceEvidenceIds,
            lineage: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["run_status"] = run.Status.ToString(),
                ["failure_type"] = report.FailureType?.ToString() ?? "none",
            });
    }

    public RuntimeEvidenceRecord RecordReview(
        TaskNode task,
        PlannerReviewArtifact artifact,
        IReadOnlyList<string>? sourceEvidenceIds = null)
    {
        var summary = $"Review for {task.TaskId} is {artifact.DecisionStatus} with resulting status {artifact.ResultingStatus}.";
        var debtDetail = artifact.DecisionDebt is null ? string.Empty : $" debt={artifact.DecisionDebt.Summary};";
        var detail = $"{summary} Verdict={artifact.Review.Verdict}; acceptance={artifact.Review.AcceptanceMet}; planner_comment={artifact.PlannerComment};{debtDetail}";
        return Append(
            RuntimeEvidenceKind.Review,
            taskId: task.TaskId,
            cardId: task.CardId,
            runId: null,
            sessionId: null,
            producer: "review-task",
            scope: $"task:{task.TaskId}",
            summary: summary,
            excerpt: PromptSafeArtifactProjectionFactory.Create(detail, detail, GetReviewArtifactRepoRelativePath(task.TaskId)),
            artifactPaths: DistinctPaths(
                new[] { GetReviewArtifactRepoRelativePath(task.TaskId) }
                    .Concat(artifact.ValidationEvidence)),
            sourceEvidenceIds: sourceEvidenceIds,
            lineage: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["review_verdict"] = artifact.Review.Verdict.ToString(),
                ["decision_status"] = artifact.DecisionStatus.ToString(),
            });
    }

    public RuntimeEvidenceRecord RecordPlanning(
        TaskNode task,
        string producer,
        string summary,
        IReadOnlyList<string> artifactPaths,
        string? runId = null,
        IReadOnlyList<string>? sourceEvidenceIds = null)
    {
        return Append(
            RuntimeEvidenceKind.Planning,
            taskId: task.TaskId,
            cardId: task.CardId,
            runId: runId,
            sessionId: null,
            producer: producer,
            scope: $"task:{task.TaskId}",
            summary: summary,
            excerpt: PromptSafeArtifactProjectionFactory.Create(summary, summary, artifactPaths.FirstOrDefault()),
            artifactPaths: DistinctPaths(artifactPaths),
            sourceEvidenceIds: sourceEvidenceIds,
            lineage: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["task_status"] = task.Status.ToString(),
                ["planner_verdict"] = task.PlannerReview.Verdict.ToString(),
            });
    }

    public IReadOnlyList<RuntimeEvidenceRecord> ListForTask(string taskId, RuntimeEvidenceKind? kind = null, int take = 100)
    {
        var files = kind is null
            ? EnumerateKinds().SelectMany(candidate => EnumerateEvidenceFiles(candidate, taskId, sessionId: null))
            : EnumerateEvidenceFiles(kind.Value, taskId, sessionId: null);
        return files
            .Select(Read)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.EvidenceId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    public RuntimeEvidenceRecord? TryGetLatest(string taskId, RuntimeEvidenceKind kind, string? runId = null)
    {
        return ListForTask(taskId, kind, take: 20)
            .FirstOrDefault(item => string.IsNullOrWhiteSpace(runId) || string.Equals(item.RunId, runId, StringComparison.Ordinal));
    }

    public RuntimeEvidenceRecord? TryGetById(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId) || !Directory.Exists(paths.EvidenceExcerptsRoot))
        {
            return null;
        }

        var path = Directory.EnumerateFiles(paths.EvidenceExcerptsRoot, $"{evidenceId}.json", SearchOption.AllDirectories).FirstOrDefault();
        return path is null ? null : Read(path);
    }

    public RuntimeEvidenceSearchResult Search(
        string? query,
        string? taskId,
        RuntimeEvidenceKind? kind,
        int budgetTokens,
        int take = 10)
    {
        var normalizedQuery = query?.Trim();
        var records = EnumerateSearchCandidates(taskId, kind)
            .Where(item => MatchesQuery(item, normalizedQuery))
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.EvidenceId, StringComparer.Ordinal)
            .Take(Math.Max(take * 4, take))
            .ToArray();

        var kept = new List<RuntimeEvidenceRecord>();
        var usedTokens = 0;
        var dropped = 0;
        foreach (var record in records)
        {
            var estimatedTokens = ContextBudgetPolicyResolver.EstimateTokens($"{record.Summary} {record.Excerpt}");
            if (kept.Count >= Math.Max(1, take) || usedTokens + estimatedTokens > budgetTokens)
            {
                dropped++;
                continue;
            }

            kept.Add(record);
            usedTokens += estimatedTokens;
        }

        return new RuntimeEvidenceSearchResult
        {
            Records = kept,
            BudgetTokens = budgetTokens,
            UsedTokens = usedTokens,
            DroppedRecords = dropped,
            TopSources = kept
                .SelectMany(item => item.ArtifactPaths.Take(1).DefaultIfEmpty(item.EvidenceId))
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray(),
        };
    }

    private RuntimeEvidenceRecord Append(
        RuntimeEvidenceKind kind,
        string? taskId,
        string? cardId,
        string? runId,
        string? sessionId,
        string producer,
        string scope,
        string summary,
        PromptSafeProjection excerpt,
        IReadOnlyList<string> artifactPaths,
        IReadOnlyList<string>? sourceEvidenceIds,
        IReadOnlyDictionary<string, string> lineage)
    {
        var root = GetPartitionRoot(kind, taskId, sessionId);
        Directory.CreateDirectory(root);
        var evidenceId = CreateEvidenceId(kind, taskId ?? sessionId ?? "repo", root);
        var record = new RuntimeEvidenceRecord
        {
            EvidenceId = evidenceId,
            Kind = kind,
            Producer = producer,
            Scope = scope,
            TaskId = taskId,
            CardId = cardId,
            RunId = runId,
            SessionId = sessionId,
            Summary = summary,
            Excerpt = excerpt.Summary,
            ExcerptTruncated = excerpt.Truncated,
            ArtifactPaths = artifactPaths,
            SourceEvidenceIds = sourceEvidenceIds?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
            Lineage = new Dictionary<string, string>(lineage, StringComparer.Ordinal),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(Path.Combine(root, $"{evidenceId}.json"), JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    private IEnumerable<string> EnumerateEvidenceFiles(RuntimeEvidenceKind kind, string? taskId, string? sessionId)
    {
        var root = GetPartitionRoot(kind, taskId, sessionId);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
    }

    private string GetPartitionRoot(RuntimeEvidenceKind kind, string? taskId, string? sessionId)
    {
        var kindFolder = kind switch
        {
            RuntimeEvidenceKind.ContextPack => "context",
            RuntimeEvidenceKind.ExecutionRun => "runs",
            RuntimeEvidenceKind.Review => "reviews",
            RuntimeEvidenceKind.Planning => "planning",
            _ => "misc",
        };

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            return Path.Combine(paths.EvidenceExcerptsRoot, kindFolder, "tasks", taskId);
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return Path.Combine(paths.EvidenceExcerptsRoot, kindFolder, "sessions", sessionId);
        }

        return Path.Combine(paths.EvidenceExcerptsRoot, kindFolder, "repo");
    }

    private static string CreateEvidenceId(RuntimeEvidenceKind kind, string partitionKey, string root)
    {
        var prefix = kind switch
        {
            RuntimeEvidenceKind.ContextPack => "CTXEVI",
            RuntimeEvidenceKind.ExecutionRun => "RUNEVI",
            RuntimeEvidenceKind.Review => "REVEVI",
            RuntimeEvidenceKind.Planning => "PLNEVI",
            _ => "EVI",
        };
        var normalizedKey = NormalizeIdToken(partitionKey);
        var next = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Count() + 1
            : 1;
        return $"{prefix}-{normalizedKey}-{next:000}";
    }

    private static string NormalizeIdToken(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static RuntimeEvidenceRecord Read(string path)
    {
        return JsonSerializer.Deserialize<RuntimeEvidenceRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Evidence record at '{path}' could not be deserialized.");
    }

    private IEnumerable<RuntimeEvidenceKind> EnumerateKinds()
    {
        yield return RuntimeEvidenceKind.ContextPack;
        yield return RuntimeEvidenceKind.ExecutionRun;
        yield return RuntimeEvidenceKind.Review;
        yield return RuntimeEvidenceKind.Planning;
    }

    private IEnumerable<RuntimeEvidenceRecord> EnumerateSearchCandidates(string? taskId, RuntimeEvidenceKind? kind)
    {
        if (!Directory.Exists(paths.EvidenceExcerptsRoot))
        {
            return Array.Empty<RuntimeEvidenceRecord>();
        }

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            return ListForTask(taskId, kind, take: 200);
        }

        var files = kind is null
            ? Directory.EnumerateFiles(paths.EvidenceExcerptsRoot, "*.json", SearchOption.AllDirectories)
            : EnumerateKindFiles(kind.Value);
        return files.Select(Read);
    }

    private IEnumerable<string> EnumerateKindFiles(RuntimeEvidenceKind kind)
    {
        var kindFolder = kind switch
        {
            RuntimeEvidenceKind.ContextPack => "context",
            RuntimeEvidenceKind.ExecutionRun => "runs",
            RuntimeEvidenceKind.Review => "reviews",
            RuntimeEvidenceKind.Planning => "planning",
            _ => "misc",
        };
        var root = Path.Combine(paths.EvidenceExcerptsRoot, kindFolder);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    private static bool MatchesQuery(RuntimeEvidenceRecord record, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return record.EvidenceId.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Kind.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Producer.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Scope.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Excerpt.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.ArtifactPaths.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
               || record.SourceEvidenceIds.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private string GetReportRepoRelativePath(string taskId, string runId)
    {
        return Path.GetRelativePath(paths.RepoRoot, Path.Combine(paths.RuntimeRoot, "run-reports", taskId, $"{runId}.json")).Replace('\\', '/');
    }

    private string GetReviewArtifactRepoRelativePath(string taskId)
    {
        return Path.GetRelativePath(paths.RepoRoot, Path.Combine(paths.ReviewArtifactsRoot, $"{taskId}.json")).Replace('\\', '/');
    }

    private IReadOnlyList<string> DistinctPaths(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeArtifactPath(value!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private string NormalizeArtifactPath(string value)
    {
        var normalized = value.Replace('\\', '/');
        if (!Path.IsPathRooted(value))
        {
            return normalized;
        }

        var relative = Path.GetRelativePath(paths.RepoRoot, value).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal)
            ? normalized
            : relative;
    }
}
