using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class OperationalHistoryArchiveReadinessService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly OperationalHistoryCompactionService compactionService;
    private readonly RuntimeArtifactCatalogService catalogService;

    public OperationalHistoryArchiveReadinessService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.paths = paths;
        compactionService = new OperationalHistoryCompactionService(repoRoot, paths, systemConfig);
        catalogService = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig);
    }

    public OperationalHistoryArchiveReadinessReport Build()
    {
        var compaction = compactionService.TryLoadLatest();
        if (compaction is null)
        {
            var empty = new OperationalHistoryArchiveReadinessReport
            {
                ArchiveRoot = ToRepoRelative(OperationalHistoryCompactionService.GetArchiveRoot(paths)),
                Summary = "No operational history compaction has been recorded yet.",
            };
            Persist(empty);
            return empty;
        }

        var catalog = catalogService.LoadOrBuild();
        var familyPolicies = catalog.Families.ToDictionary(family => family.FamilyId, StringComparer.Ordinal);
        var index = compactionService.LoadArchiveIndex();
        var families = compaction.Families
            .Select(family => BuildFamilyProjection(family, index, familyPolicies))
            .OrderByDescending(family => family.PromotionRelevantCount)
            .ThenByDescending(family => family.ArchivedFileCount)
            .ToArray();
        var promotionRelevantEntries = index.Entries
            .Where(entry => IsPromotionRelevant(entry).PromotionRelevant)
            .OrderByDescending(entry => entry.ArchivedAt)
            .Take(20)
            .Select(entry => BuildPromotionRelevantEntry(entry, familyPolicies))
            .ToArray();

        var report = new OperationalHistoryArchiveReadinessReport
        {
            ArchiveRoot = compaction.ArchiveRoot,
            SourceCompactionReportId = compaction.ReportId,
            Families = families,
            PromotionRelevantEntries = promotionRelevantEntries,
            Summary = BuildSummary(compaction, promotionRelevantEntries),
        };
        Persist(report);
        return report;
    }

    public OperationalHistoryArchiveReadinessReport? TryLoadLatest()
    {
        var path = GetLatestReportPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OperationalHistoryArchiveReadinessReport>(File.ReadAllText(path), JsonOptions);
    }

    public static string GetLatestReportPath(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "archive-readiness.json");
    }

    private OperationalHistoryArchiveReadinessFamily BuildFamilyProjection(
        OperationalHistoryCompactionFamily family,
        OperationalHistoryArchiveIndex index,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyPolicies)
    {
        familyPolicies.TryGetValue(family.FamilyId, out var policy);
        var familyEntries = index.Entries
            .Where(entry => string.Equals(entry.FamilyId, family.FamilyId, StringComparison.Ordinal))
            .ToArray();
        var promotionRelevantCount = familyEntries.Count(entry => IsPromotionRelevant(entry).PromotionRelevant);
        var archiveReason = ResolveArchiveReason(policy);
        return new OperationalHistoryArchiveReadinessFamily
        {
            FamilyId = family.FamilyId,
            DisplayName = policy?.DisplayName ?? family.FamilyId,
            RetentionMode = policy?.RetentionMode ?? RuntimeArtifactRetentionMode.RollingWindow,
            HotWindowCount = policy?.Budget.HotWindowCount,
            MaxAgeDays = policy?.Budget.MaxAgeDays,
            RetentionDiscipline = policy is null
                ? "rolling_window"
                : RuntimeCommitHygieneService.DescribeRetentionDiscipline(policy),
            ClosureDiscipline = policy is null
                ? "compact_history"
                : RuntimeCommitHygieneService.DescribeClosureDiscipline(policy),
            ArchiveReadinessState = policy is null
                ? "archive_ready_when_compaction_runs"
                : RuntimeCommitHygieneService.DescribeArchiveReadinessState(policy),
            ArchivedFileCount = family.ArchivedFileCount,
            PreservedHotFileCount = family.PreservedHotFileCount,
            ArchiveReason = archiveReason,
            PromotionRelevantCount = promotionRelevantCount,
            Summary = $"{policy?.DisplayName ?? family.FamilyId}: archived {family.ArchivedFileCount}, preserved hot {family.PreservedHotFileCount}, promotion-relevant {promotionRelevantCount}.",
        };
    }

    private OperationalHistoryArchiveReadinessEntry BuildPromotionRelevantEntry(
        OperationalHistoryArchiveEntry entry,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyPolicies)
    {
        var relevance = IsPromotionRelevant(entry);
        familyPolicies.TryGetValue(entry.FamilyId, out var policy);
        return new OperationalHistoryArchiveReadinessEntry
        {
            EntryId = entry.EntryId,
            FamilyId = entry.FamilyId,
            OriginalPath = entry.OriginalPath,
            ArchivedPath = entry.ArchivedPath,
            ArchivedAt = entry.ArchivedAt,
            WhyArchived = ResolveArchiveReason(policy),
            ArchiveReadinessState = policy is null
                ? "archive_ready_when_compaction_runs"
                : RuntimeCommitHygieneService.DescribeArchiveReadinessState(policy),
            PromotionRelevant = relevance.PromotionRelevant,
            PromotionReason = relevance.PromotionReason,
        };
    }

    private static (bool PromotionRelevant, string PromotionReason) IsPromotionRelevant(OperationalHistoryArchiveEntry entry)
    {
        var normalizedPath = entry.OriginalPath.Replace('\\', '/');
        if (string.Equals(entry.FamilyId, "worker_execution_artifact_history", StringComparison.Ordinal))
        {
            if (normalizedPath.Contains("/provider/", StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Provider execution artifacts can still matter for promotion and route-comparison evidence.");
            }

            if (normalizedPath.Contains("/reviews/", StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Review artifacts can still matter for milestone or promotion review evidence.");
            }
        }

        return (false, "No promotion signal projected from this archived entry.");
    }

    private static string ResolveArchiveReason(RuntimeArtifactFamilyPolicy? policy)
    {
        if (policy is null)
        {
            return "Archived by operational history compaction.";
        }

        if (policy.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow && policy.Budget.HotWindowCount is not null)
        {
            return $"Archived because it fell outside the configured hot window of {policy.Budget.HotWindowCount.Value} online item(s).";
        }

        if (policy.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow)
        {
            return "Archived because it fell outside the configured online retention window.";
        }

        return "Archived by operational history compaction.";
    }

    private static string BuildSummary(
        OperationalHistoryCompactionReport compaction,
        IReadOnlyCollection<OperationalHistoryArchiveReadinessEntry> promotionRelevantEntries)
    {
        return $"Projected archive readiness from compaction report {compaction.ReportId}: archived {compaction.ArchivedFileCount} file(s), preserved {compaction.PreservedHotFileCount} hot-window file(s), promotion-relevant archived entries {promotionRelevantEntries.Count}.";
    }

    private void Persist(OperationalHistoryArchiveReadinessReport report)
    {
        var path = GetLatestReportPath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(paths.RepoRoot, Path.GetFullPath(path)).Replace('\\', '/');
    }
}
