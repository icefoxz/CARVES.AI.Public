using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ContextShow(string taskId)
    {
        return InspectContextPack(taskId);
    }

    public OperatorCommandResult ContextEstimate(string taskId, string? model = null, int? overrideMaxContextTokens = null)
    {
        var task = taskGraphService.GetTask(taskId);
        var pack = contextPackService.BuildForTask(task, model ?? aiProviderConfig.Model, overrideMaxContextTokens);
        var telemetry = new ContextBudgetTelemetryService(paths)
            .ListRecent(20)
            .FirstOrDefault(item =>
                string.Equals(item.OperationKind, "context_pack_build", StringComparison.Ordinal)
                && string.Equals(item.TaskId, taskId, StringComparison.Ordinal));

        var lines = new List<string>
        {
            $"Context estimate for {taskId}:",
            $"Profile/model: {pack.Budget.ProfileId} / {pack.Budget.Model}",
            $"Budget posture: {pack.Budget.BudgetPosture}",
            $"Budget: used={pack.Budget.UsedTokens}/{pack.Budget.MaxContextTokens} target={pack.Budget.TargetTokens} advisory={pack.Budget.AdvisoryTokens} hard_safety={pack.Budget.HardSafetyTokens}",
            $"Fixed/dynamic/total: {pack.Budget.FixedTokensEstimate} / {pack.Budget.DynamicTokensEstimate} / {pack.Budget.TotalContextTokensEstimate}",
            $"L3 queries/evidence expansions: {pack.Budget.L3QueryCount} / {pack.Budget.EvidenceExpansionCount}",
            $"Truncated/dropped/full_doc_blocked: {pack.Budget.TruncatedItemsCount} / {pack.Budget.DroppedItemsCount} / {pack.Budget.FullDocBlockedCount}",
            $"Recall items/modules/expandable refs: {pack.Recall.Count} / {pack.RelevantModules.Count} / {pack.ExpandableReferences.Count}",
            $"Telemetry: {(telemetry is null ? "(pending)" : telemetry.TelemetryId)}",
            string.Empty,
            "Reason codes:",
        };

        if (pack.Budget.BudgetViolationReasonCodes.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Budget.BudgetViolationReasonCodes.Select(item => $"- {item}"));
        }

        lines.Add(string.Empty);
        lines.Add("Top sources:");
        if (pack.Budget.TopSources.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Budget.TopSources.Select(item => $"- {item}"));
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult EvidenceSearch(string? query, string? taskId, string? kind, int budgetTokens, int take)
    {
        var evidenceStore = new RuntimeEvidenceStoreService(paths);
        var parsedKind = ParseRuntimeEvidenceKind(kind);
        var result = evidenceStore.Search(query, taskId, parsedKind, budgetTokens, take);
        var telemetry = new ContextBudgetTelemetryService(paths).RecordSearch(
            operationKind: "evidence_search",
            profileId: "evidence_search",
            queryText: query ?? taskId,
            budgetTokens: budgetTokens,
            usedTokens: result.UsedTokens,
            resultCount: result.Records.Count,
            droppedItemsCount: result.DroppedRecords,
            topSources: result.TopSources,
            outcome: result.Records.Count == 0 ? "no_results" : "results_returned",
            evidenceExpansionCount: result.Records.Count(item => item.Kind == RuntimeEvidenceKind.ExecutionRun || item.Kind == RuntimeEvidenceKind.Review));

        if (result.Records.Count == 0)
        {
            return OperatorCommandResult.Failure(
                $"Evidence search returned no matches.",
                $"Query: {query ?? "(none)"}",
                $"Task: {taskId ?? "(none)"}",
                $"Kind: {kind ?? "(all)"}",
                $"Telemetry: {telemetry.TelemetryId}");
        }

        var lines = new List<string>
        {
            $"Evidence search: query={query ?? "(none)"} task={taskId ?? "(none)"} kind={kind ?? "(all)"}",
            $"Budget: used={result.UsedTokens}/{result.BudgetTokens} tokens; returned={result.Records.Count}; dropped={result.DroppedRecords}",
            $"Telemetry: {telemetry.TelemetryId}",
            string.Empty,
        };
        foreach (var record in result.Records)
        {
            lines.Add($"- {record.EvidenceId} [{record.Kind}] scope={record.Scope} producer={record.Producer}");
            lines.Add($"  summary: {record.Summary}");
            lines.Add($"  excerpt: {record.Excerpt}");
            lines.Add($"  artifacts: {(record.ArtifactPaths.Count == 0 ? "(none)" : string.Join(", ", record.ArtifactPaths))}");
            lines.Add($"  source_evidence_ids: {(record.SourceEvidenceIds.Count == 0 ? "(none)" : string.Join(", ", record.SourceEvidenceIds))}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult MemorySearch(string query, string? category, string? scope, int budgetTokens, bool includeInactiveFacts)
    {
        var hits = new List<MemorySearchHit>();
        var activeMemoryReadService = new ActiveMemoryReadService(paths);
        var categories = string.IsNullOrWhiteSpace(category)
            ? new[] { "architecture", "project", "modules", "patterns" }
            : new[] { category };
        foreach (var selectedCategory in categories)
        {
            foreach (var document in activeMemoryReadService.LoadCompatibleDocuments(selectedCategory))
            {
                if (!MemoryDocumentMatches(document, query, scope))
                {
                    continue;
                }

                var projection = PromptSafeArtifactProjectionFactory.Create(document.Content, document.Content, document.Path);
                hits.Add(new MemorySearchHit(
                    $"doc:{document.Path}",
                    "document",
                    document.Path,
                    document.Title,
                    selectedCategory,
                    projection.Summary,
                    ContextBudgetPolicyResolver.EstimateTokens(projection.Summary),
                    null));
            }
        }

        var scopeHints = string.IsNullOrWhiteSpace(scope)
            ? Array.Empty<string>()
            : new[] { scope };
        var facts = activeMemoryReadService.ListFacts(scopeHints, category, includeInactiveFacts, take: 200);
        foreach (var fact in facts)
        {
            if (!MemoryFactMatches(fact, query, category, scope))
            {
                continue;
            }

            var projection = PromptSafeArtifactProjectionFactory.Create(fact.Statement, fact.Statement, fact.TargetMemoryPath ?? fact.FactId);
            hits.Add(new MemorySearchHit(
                $"fact:{fact.FactId}",
                "fact",
                fact.TargetMemoryPath ?? $".ai/evidence/facts/{fact.FactId}.json",
                fact.Title,
                fact.Category,
                projection.Summary,
                ContextBudgetPolicyResolver.EstimateTokens(projection.Summary),
                fact.Status.ToString().ToLowerInvariant()));
        }

        var ordered = hits
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToArray();
        var kept = new List<MemorySearchHit>();
        var usedTokens = 0;
        var dropped = 0;
        foreach (var hit in ordered)
        {
            if (usedTokens + hit.TokenEstimate > budgetTokens)
            {
                dropped++;
                continue;
            }

            kept.Add(hit);
            usedTokens += hit.TokenEstimate;
        }

        var telemetry = new ContextBudgetTelemetryService(paths).RecordSearch(
            operationKind: "memory_search",
            profileId: "memory_search",
            queryText: query,
            budgetTokens: budgetTokens,
            usedTokens: usedTokens,
            resultCount: kept.Count,
            droppedItemsCount: dropped,
            topSources: kept.Select(item => item.Source).Distinct(StringComparer.Ordinal).Take(5).ToArray(),
            outcome: kept.Count == 0 ? "no_results" : "results_returned");

        if (kept.Count == 0)
        {
            return OperatorCommandResult.Failure(
                $"Memory search returned no matches for '{query}'.",
                $"Category: {category ?? "(all)"}",
                $"Scope: {scope ?? "(none)"}",
                $"Telemetry: {telemetry.TelemetryId}");
        }

        var lines = new List<string>
        {
            $"Memory search: {query}",
            $"Category/scope: {category ?? "(all)"} / {scope ?? "(none)"}",
            $"Budget: used={usedTokens}/{budgetTokens} tokens; returned={kept.Count}; dropped={dropped}",
            $"Telemetry: {telemetry.TelemetryId}",
            string.Empty,
        };
        foreach (var hit in kept)
        {
            lines.Add($"- {hit.Kind}: {hit.Source} [{hit.Category}] tokens={hit.TokenEstimate}");
            lines.Add($"  title: {hit.Title}");
            if (!string.IsNullOrWhiteSpace(hit.Status))
            {
                lines.Add($"  status: {hit.Status}");
            }

            lines.Add($"  excerpt: {hit.Excerpt}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult MemoryPromoteFromEvidence(
        string evidenceId,
        string? category,
        string? title,
        string? summary,
        string? statement,
        string? scope,
        string? targetMemoryPath,
        string? taskScope,
        string? commitScope,
        double confidence,
        bool canonical,
        IReadOnlyList<string> supersedes,
        string actor)
    {
        var evidenceStore = new RuntimeEvidenceStoreService(paths);
        var evidence = evidenceStore.TryGetById(evidenceId);
        if (evidence is null)
        {
            return OperatorCommandResult.Failure($"Evidence '{evidenceId}' was not found.");
        }

        var effectiveCategory = string.IsNullOrWhiteSpace(category) ? "project" : category;
        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? Truncate(evidence.Summary, 80) : title;
        var effectiveSummary = string.IsNullOrWhiteSpace(summary) ? evidence.Summary : summary;
        var effectiveStatement = string.IsNullOrWhiteSpace(statement) ? evidence.Excerpt : statement;
        var effectiveScope = string.IsNullOrWhiteSpace(scope) ? evidence.Scope : scope;
        var promotionService = new RuntimeMemoryPromotionService(paths);
        var candidate = promotionService.StageCandidate(
            effectiveCategory,
            effectiveTitle,
            effectiveSummary,
            effectiveStatement,
            effectiveScope,
            proposer: actor,
            sourceEvidenceIds: new[] { evidence.EvidenceId },
            confidence: confidence,
            targetMemoryPath: targetMemoryPath,
            taskScope: taskScope ?? evidence.TaskId,
            commitScope: commitScope);
        var candidateAudit = promotionService.RecordCandidateAudit(candidate.CandidateId, MemoryPromotionAuditDecision.Approved, actor, "host memory promote command approved candidate promotion.");
        var provisional = promotionService.PromoteCandidateToProvisional(candidate.CandidateId, candidateAudit.AuditId, actor);

        var lines = new List<string>
        {
            $"Memory promote from evidence {evidenceId}:",
            $"Candidate: {candidate.CandidateId}",
            $"Candidate audit: {candidateAudit.AuditId}",
            $"Provisional fact: {provisional.FactId}",
        };

        if (canonical)
        {
            var factAudit = promotionService.RecordFactAudit(provisional.FactId, MemoryPromotionAuditDecision.Approved, actor, "host memory promote command approved canonical promotion.");
            var canonicalFact = promotionService.PromoteFactToCanonical(provisional.FactId, factAudit.AuditId, actor, supersedes);
            lines.Add($"Canonical audit: {factAudit.AuditId}");
            lines.Add($"Canonical fact: {canonicalFact.FactId}");
            lines.Add($"Supersedes: {(canonicalFact.Supersedes.Count == 0 ? "(none)" : string.Join(", ", canonicalFact.Supersedes))}");
        }

        lines.Add($"Evidence source: {evidence.EvidenceId}");
        lines.Add($"Target path: {candidate.TargetMemoryPath ?? "(none)"}");
        return new OperatorCommandResult(0, lines);
    }

    private IReadOnlyList<Carves.Runtime.Domain.Memory.MemoryDocument> LoadMemoryDocuments(string category)
    {
        var root = Path.Combine(paths.AiRoot, "memory", category);
        if (!Directory.Exists(root))
        {
            return Array.Empty<Carves.Runtime.Domain.Memory.MemoryDocument>();
        }

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new Carves.Runtime.Domain.Memory.MemoryDocument(
                Path.GetRelativePath(paths.RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                category,
                Path.GetFileNameWithoutExtension(path),
                File.ReadAllText(path)))
            .ToArray();
    }

    private static RuntimeEvidenceKind? ParseRuntimeEvidenceKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<RuntimeEvidenceKind>(normalized, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static bool MemoryDocumentMatches(Carves.Runtime.Domain.Memory.MemoryDocument document, string query, string? scope)
    {
        var scopeMatches = string.IsNullOrWhiteSpace(scope)
            || document.Path.Contains(scope, StringComparison.OrdinalIgnoreCase)
            || document.Category.Contains(scope, StringComparison.OrdinalIgnoreCase);
        if (!scopeMatches)
        {
            return false;
        }

        return document.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
               || document.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || document.Content.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MemoryFactMatches(TemporalMemoryFactRecord fact, string query, string? category, string? scope)
    {
        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(fact.Category, category, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(scope)
            && !string.Equals(fact.Scope, scope, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fact.TaskScope, scope, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fact.FactId.Contains(query, StringComparison.OrdinalIgnoreCase)
               || fact.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || fact.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
               || fact.Statement.Contains(query, StringComparison.OrdinalIgnoreCase)
               || fact.Scope.Contains(query, StringComparison.OrdinalIgnoreCase)
               || (fact.TargetMemoryPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }

    private sealed record MemorySearchHit(
        string Id,
        string Kind,
        string Source,
        string Title,
        string Category,
        string Excerpt,
        int TokenEstimate,
        string? Status);
}
