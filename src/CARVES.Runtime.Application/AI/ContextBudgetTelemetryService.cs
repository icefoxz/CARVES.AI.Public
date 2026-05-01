using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class ContextBudgetTelemetryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public ContextBudgetTelemetryService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public ContextBudgetTelemetryRecord RecordContextPack(ContextPack pack, string outcome)
    {
        return Append(new ContextBudgetTelemetryRecord
        {
            OperationKind = "context_pack_build",
            ProfileId = pack.Budget.ProfileId,
            ModelId = pack.Budget.Model,
            EstimatorVersion = pack.Budget.EstimatorVersion,
            FixedTokensEst = pack.Budget.FixedTokensEstimate,
            DynamicTokensEst = pack.Budget.DynamicTokensEstimate,
            TotalContextTokensEst = pack.Budget.TotalContextTokensEstimate,
            BudgetPosture = pack.Budget.BudgetPosture,
            BudgetViolationReason = pack.Budget.BudgetViolationReasonCodes,
            L3QueryCount = pack.Budget.L3QueryCount,
            EvidenceExpansionCount = pack.Budget.EvidenceExpansionCount,
            TruncatedItemsCount = pack.Budget.TruncatedItemsCount,
            DroppedItemsCount = pack.Budget.DroppedItemsCount,
            FullDocBlockedCount = pack.Budget.FullDocBlockedCount,
            TopSources = pack.Budget.TopSources,
            Outcome = outcome,
            PackId = pack.PackId,
            Audience = pack.Audience.ToString(),
            ArtifactPath = pack.ArtifactPath,
            FacetPhase = pack.FacetNarrowing.Phase,
            TaskId = pack.TaskId,
            ResultCount = pack.Recall.Count + pack.RelevantModules.Count + pack.ExpandableReferences.Count,
            IncludedItemIds = BuildIncludedItemIds(pack),
            TrimmedItems = pack.Trimmed
                .Select(item => new ContextBudgetTelemetryTrimmedItem
                {
                    ItemId = item.Key,
                    Layer = item.Layer.ToString(),
                    Priority = item.Priority.ToString(),
                    EstimatedTokens = item.EstimatedTokens,
                    Reason = item.Reason,
                })
                .ToArray(),
            ExpandableReferences = pack.ExpandableReferences
                .Select(reference => new ContextBudgetTelemetryExpandableReference
                {
                    Kind = reference.Kind,
                    Path = reference.Path,
                    Layer = reference.Layer.ToString(),
                })
                .ToArray(),
            SourcePaths = BuildSourcePaths(pack),
        });
    }

    public ContextBudgetTelemetryRecord RecordSearch(
        string operationKind,
        string profileId,
        string? queryText,
        int budgetTokens,
        int usedTokens,
        int resultCount,
        int droppedItemsCount,
        IReadOnlyList<string> topSources,
        string outcome,
        int evidenceExpansionCount = 0)
    {
        var posture = usedTokens > budgetTokens
            ? ContextBudgetPostures.HardCapEnforced
            : usedTokens > Math.Max(1, budgetTokens * 4 / 5)
                ? ContextBudgetPostures.OverAdvisory
                : ContextBudgetPostures.WithinTarget;
        var reasons = usedTokens > budgetTokens
            ? new[] { ContextBudgetReasonCodes.EvidenceRequired }
            : Array.Empty<string>();
        return Append(new ContextBudgetTelemetryRecord
        {
            OperationKind = operationKind,
            ProfileId = profileId,
            ModelId = "n/a",
            EstimatorVersion = ContextBudgetPolicyResolver.EstimatorVersion,
            FixedTokensEst = 0,
            DynamicTokensEst = usedTokens,
            TotalContextTokensEst = usedTokens,
            BudgetPosture = posture,
            BudgetViolationReason = reasons,
            L3QueryCount = 1,
            EvidenceExpansionCount = evidenceExpansionCount,
            TruncatedItemsCount = 0,
            DroppedItemsCount = droppedItemsCount,
            FullDocBlockedCount = 0,
            TopSources = topSources,
            Outcome = outcome,
            QueryText = queryText,
            BudgetTokens = budgetTokens,
            ResultCount = resultCount,
        });
    }

    public IReadOnlyList<ContextBudgetTelemetryRecord> ListRecent(int take = 50)
    {
        if (!Directory.Exists(paths.RuntimeContextBudgetsRoot))
        {
            return Array.Empty<ContextBudgetTelemetryRecord>();
        }

        return Directory.EnumerateFiles(paths.RuntimeContextBudgetsRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Read)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.TelemetryId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    private ContextBudgetTelemetryRecord Append(ContextBudgetTelemetryRecord record)
    {
        Directory.CreateDirectory(paths.RuntimeContextBudgetsRoot);
        var telemetry = record with
        {
            TelemetryId = CreateSequentialId("CTXTEL", paths.RuntimeContextBudgetsRoot),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(Path.Combine(paths.RuntimeContextBudgetsRoot, $"{telemetry.TelemetryId}.json"), JsonSerializer.Serialize(telemetry, JsonOptions));
        return telemetry;
    }

    private static ContextBudgetTelemetryRecord Read(string path)
    {
        return JsonSerializer.Deserialize<ContextBudgetTelemetryRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Context budget telemetry at '{path}' could not be deserialized.");
    }

    private static string CreateSequentialId(string prefix, string root)
    {
        Directory.CreateDirectory(root);
        var next = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Count() + 1;
        return $"{prefix}-{next:000}";
    }

    private static IReadOnlyList<string> BuildIncludedItemIds(ContextPack pack)
    {
        var ids = new List<string>();
        AddIfPresent(ids, "goal", pack.Goal);
        AddIfPresent(ids, "task", pack.Task);

        for (var index = 0; index < pack.Constraints.Count; index++)
        {
            ids.Add($"constraint:{index + 1}");
        }

        if (pack.AcceptanceContract is not null)
        {
            ids.Add($"acceptance_contract:{pack.AcceptanceContract.ContractId}");
        }

        foreach (var dependency in pack.LocalTaskGraph.Dependencies)
        {
            ids.Add($"dependency:{dependency.TaskId}");
        }

        foreach (var blocker in pack.LocalTaskGraph.Blockers)
        {
            ids.Add($"blocker:{blocker.TaskId}");
        }

        foreach (var module in pack.RelevantModules)
        {
            ids.Add($"module:{module.Module}");
        }

        foreach (var recall in pack.Recall)
        {
            ids.Add($"recall:{recall.Kind}:{recall.Source}");
        }

        for (var index = 0; index < pack.CodeHints.Count; index++)
        {
            ids.Add($"code_hint:{index + 1}");
        }

        foreach (var window in pack.WindowedReads)
        {
            ids.Add($"windowed_read:{window.Path}:{window.StartLine}-{window.EndLine}");
        }

        if (pack.LastFailureSummary is not null)
        {
            ids.Add("last_failure_summary");
        }

        if (pack.LastRunSummary is not null)
        {
            ids.Add($"last_run_summary:{pack.LastRunSummary.RunId}");
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildSourcePaths(ContextPack pack)
    {
        var paths = new List<string>();
        if (pack.ArtifactPath is not null)
        {
            AddIfPresent(paths, pack.ArtifactPath);
        }
        paths.AddRange(pack.RelevantModules.SelectMany(module => module.Files));
        paths.AddRange(pack.Recall.Select(recall => recall.Source));
        paths.AddRange(pack.WindowedReads.Select(window => window.Path));
        paths.AddRange(pack.ExpandableReferences.Select(reference => reference.Path));
        paths.AddRange(pack.Budget.TopSources.Where(IsPathLike));

        return paths
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddIfPresent(ICollection<string> items, string item)
    {
        if (!string.IsNullOrWhiteSpace(item))
        {
            items.Add(item);
        }
    }

    private static void AddIfPresent(ICollection<string> items, string itemId, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            items.Add(itemId);
        }
    }

    private static bool IsPathLike(string value)
    {
        return value.Contains('/', StringComparison.Ordinal)
               || value.Contains('\\', StringComparison.Ordinal)
               || value.StartsWith(".", StringComparison.Ordinal);
    }
}
