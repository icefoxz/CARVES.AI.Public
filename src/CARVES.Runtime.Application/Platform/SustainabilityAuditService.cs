using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class SustainabilityAuditService
{
    private static readonly string[] CanonicalLeakExtensions =
    [
        ".log",
        ".diff",
        ".patch",
        ".stdout",
        ".stderr",
        ".tmp",
    ];

    private static readonly string[] DerivedForbiddenMarkers =
    [
        "/bin/",
        "/obj/",
        "/testresults/",
        "/coverage/",
        ".deps.json",
        ".runtimeconfig.json",
        ".nuget.dgspec.json",
        "project.assets.json",
        ".assemblyinfo.cs",
        ".globalusings.g.cs",
        ".assemblyattributes.cs",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly ICodeGraphQueryService codeGraphQueryService;
    private readonly RuntimeArtifactCatalogService catalogService;

    public SustainabilityAuditService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        ICodeGraphQueryService codeGraphQueryService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.codeGraphQueryService = codeGraphQueryService;
        catalogService = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig);
    }

    public SustainabilityAuditReport Audit()
    {
        var catalog = catalogService.LoadOrBuild();
        var findings = new List<SustainabilityAuditFinding>();
        var projections = new List<RuntimeArtifactBudgetProjection>();

        foreach (var family in catalog.Families)
        {
            var observation = ObserveFamily(family);
            var recommendedAction = ResolveRecommendedAction(family, observation);
            projections.Add(new RuntimeArtifactBudgetProjection
            {
                FamilyId = family.FamilyId,
                DisplayName = family.DisplayName,
                ArtifactClass = family.ArtifactClass,
                RetentionMode = family.RetentionMode,
                DefaultReadVisibility = family.DefaultReadVisibility,
                HotWindowCount = family.Budget.HotWindowCount,
                MaxAgeDays = family.Budget.MaxAgeDays,
                RetentionDiscipline = RuntimeCommitHygieneService.DescribeRetentionDiscipline(family),
                ClosureDiscipline = RuntimeCommitHygieneService.DescribeClosureDiscipline(family),
                ArchiveReadinessState = RuntimeCommitHygieneService.DescribeArchiveReadinessState(family),
                FileCount = observation.FileCount,
                TotalBytes = observation.TotalBytes,
                RetentionOverdueCount = observation.RetentionOverdueCount,
                ReadPathPressureCount = observation.ReadPathPressureCount,
                HotWindowExcessCount = observation.HotWindowExcessCount,
                OldestItemAgeDays = observation.OldestItemAgeDays,
                OverFileBudget = observation.OverFileBudget,
                OverByteBudget = observation.OverByteBudget,
                WithinBudget = observation.WithinBudget,
                RecommendedAction = recommendedAction,
                Summary = BuildProjectionSummary(family, observation, recommendedAction),
            });

            findings.AddRange(BuildFindings(family, observation, recommendedAction));
        }

        findings.AddRange(ScanCodeGraphResidueFindings());

        var codeGraphAudit = new CodeGraphAuditService(repoRoot, paths, systemConfig, codeGraphQueryService).Audit();
        if (!codeGraphAudit.StrictPassed)
        {
            findings.AddRange(codeGraphAudit.Findings.Select(finding => new SustainabilityAuditFinding
            {
                Category = "codegraph_pollution",
                Severity = string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase) ? "error" : "warning",
                FamilyId = "codegraph_derived",
                Path = finding.Path,
                Message = finding.Message,
                RecommendedAction = RuntimeMaintenanceActionKind.RebuildDerived,
            }));
        }

        var report = new SustainabilityAuditReport
        {
            CatalogId = catalog.CatalogId,
            StrictPassed = findings.All(finding => !string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            Findings = findings
                .OrderByDescending(finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .ThenBy(finding => finding.FamilyId, StringComparer.Ordinal)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Families = projections
                .OrderBy(projection => projection.ArtifactClass)
                .ThenBy(projection => projection.FamilyId, StringComparer.Ordinal)
                .ToArray(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(GetAuditPath(paths))!);
        File.WriteAllText(GetAuditPath(paths), JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public SustainabilityAuditReport? TryLoadLatest()
    {
        var path = GetAuditPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SustainabilityAuditReport>(File.ReadAllText(path), JsonOptions);
    }

    public static string GetAuditPath(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "audit.json");
    }

    private RuntimeArtifactObservation ObserveFamily(RuntimeArtifactFamilyPolicy family)
    {
        var files = ResolveFiles(family);
        var now = DateTimeOffset.UtcNow;
        var budget = family.Budget;
        var fileCount = files.Count;
        var totalBytes = files.Sum(file => file.Length);
        var oldest = files.Count == 0
            ? (int?)null
            : (int)Math.Floor((now - files.Min(file => file.LastWriteUtc)).TotalDays);
        var retentionOverdue = budget.MaxAgeDays is null
            ? 0
            : files.Count(file => (now - file.LastWriteUtc).TotalDays > budget.MaxAgeDays.Value);
        var hotWindowExcess = budget.HotWindowCount is null
            ? 0
            : Math.Max(0, fileCount - budget.HotWindowCount.Value);
        var overFileBudget = budget.MaxOnlineFiles is not null && fileCount > budget.MaxOnlineFiles.Value;
        var overByteBudget = budget.MaxOnlineBytes is not null && totalBytes > budget.MaxOnlineBytes.Value;
        var readPathPressure = family.DefaultReadVisibility is RuntimeArtifactReadVisibility.Summary
            ? (overFileBudget ? 1 : 0) + (overByteBudget ? 1 : 0)
            : 0;
        var withinBudget = !overFileBudget && !overByteBudget && retentionOverdue == 0 && hotWindowExcess == 0;

        return new RuntimeArtifactObservation(
            fileCount,
            totalBytes,
            oldest,
            retentionOverdue,
            readPathPressure,
            hotWindowExcess,
            overFileBudget,
            overByteBudget,
            withinBudget,
            files);
    }

    private List<ResolvedArtifactFile> ResolveFiles(RuntimeArtifactFamilyPolicy family)
    {
        var files = new List<ResolvedArtifactFile>();
        foreach (var root in family.Roots)
        {
            var absoluteRoot = Path.IsPathRooted(root) ? root : Path.GetFullPath(Path.Combine(repoRoot, root));
            if (File.Exists(absoluteRoot))
            {
                var fileInfo = new FileInfo(absoluteRoot);
                files.Add(new ResolvedArtifactFile(ToRepoRelative(fileInfo.FullName), fileInfo.Length, fileInfo.LastWriteTimeUtc));
                continue;
            }

            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                var fileInfo = new FileInfo(file);
                var relativePath = ToRepoRelative(fileInfo.FullName);
                if (!ShouldIncludeFileInFamily(family, relativePath))
                {
                    continue;
                }

                files.Add(new ResolvedArtifactFile(relativePath, fileInfo.Length, fileInfo.LastWriteTimeUtc));
            }
        }

        return files;
    }

    private IEnumerable<SustainabilityAuditFinding> BuildFindings(
        RuntimeArtifactFamilyPolicy family,
        RuntimeArtifactObservation observation,
        RuntimeMaintenanceActionKind recommendedAction)
    {
        var findings = new List<SustainabilityAuditFinding>();
        if (family.ArtifactClass == RuntimeArtifactClass.CanonicalTruth)
        {
            findings.AddRange(observation.Files
                .Where(file => CanonicalLeakExtensions.Any(extension => file.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                .Select(file => new SustainabilityAuditFinding
                {
                    Category = "canonical_truth_pollution",
                    Severity = "error",
                    FamilyId = family.FamilyId,
                    Path = file.RelativePath,
                    Message = $"Canonical truth contains a raw operational artifact '{Path.GetExtension(file.RelativePath)}'.",
                    RecommendedAction = file.RelativePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                        ? RuntimeMaintenanceActionKind.PruneEphemeral
                        : RuntimeMaintenanceActionKind.None,
                }));
        }

        if (family.ArtifactClass == RuntimeArtifactClass.DerivedTruth)
        {
            findings.AddRange(observation.Files
                .Where(file => DerivedForbiddenMarkers.Any(marker => file.RelativePath.Replace('\\', '/').ToLowerInvariant().Contains(marker)))
                .Select(file => new SustainabilityAuditFinding
                {
                    Category = "derived_truth_pollution",
                    Severity = "error",
                    FamilyId = family.FamilyId,
                    Path = file.RelativePath,
                    Message = "Derived truth contains generated/build residue that should stay outside the derived read path.",
                    RecommendedAction = RuntimeMaintenanceActionKind.RebuildDerived,
                }));
        }

        if (observation.OverFileBudget)
        {
            findings.Add(new SustainabilityAuditFinding
            {
                Category = "growth_budget_exceeded",
                Severity = family.ArtifactClass == RuntimeArtifactClass.CanonicalTruth ? "warning" : "warning",
                FamilyId = family.FamilyId,
                Path = string.Join(", ", family.Roots),
                Message = $"File count {observation.FileCount} exceeds budget {family.Budget.MaxOnlineFiles}.",
                RecommendedAction = recommendedAction,
            });
        }

        if (observation.OverByteBudget)
        {
            findings.Add(new SustainabilityAuditFinding
            {
                Category = "size_budget_exceeded",
                Severity = family.DefaultReadVisibility == RuntimeArtifactReadVisibility.Hidden ? "warning" : "warning",
                FamilyId = family.FamilyId,
                Path = string.Join(", ", family.Roots),
                Message = $"Total bytes {observation.TotalBytes} exceed budget {family.Budget.MaxOnlineBytes}.",
                RecommendedAction = recommendedAction,
            });
        }

        if (observation.RetentionOverdueCount > 0)
        {
            findings.Add(new SustainabilityAuditFinding
            {
                Category = "retention_drift",
                Severity = family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue ? "error" : "warning",
                FamilyId = family.FamilyId,
                Path = string.Join(", ", family.Roots),
                Message = $"{observation.RetentionOverdueCount} item(s) exceeded the configured online age budget.",
                RecommendedAction = recommendedAction,
            });
        }

        if (observation.ReadPathPressureCount > 0)
        {
            findings.Add(new SustainabilityAuditFinding
            {
                Category = "read_path_pressure",
                Severity = "warning",
                FamilyId = family.FamilyId,
                Path = string.Join(", ", family.Roots),
                Message = "A default-readable family is above its online budget and is making the read path heavier than intended.",
                RecommendedAction = recommendedAction,
            });
        }

        return findings;
    }

    private static RuntimeMaintenanceActionKind ResolveRecommendedAction(RuntimeArtifactFamilyPolicy family, RuntimeArtifactObservation observation)
    {
        if (family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue
            && (observation.FileCount > 0 || observation.RetentionOverdueCount > 0))
        {
            return RuntimeMaintenanceActionKind.PruneEphemeral;
        }

        if (family.CompactEligible
            && (observation.RetentionOverdueCount > 0 || observation.HotWindowExcessCount > 0 || observation.OverFileBudget || observation.OverByteBudget))
        {
            return RuntimeMaintenanceActionKind.CompactHistory;
        }

        if (family.ArtifactClass == RuntimeArtifactClass.DerivedTruth
            && family.RebuildEligible
            && (observation.OverFileBudget || observation.OverByteBudget || observation.RetentionOverdueCount > 0))
        {
            return RuntimeMaintenanceActionKind.RebuildDerived;
        }

        return RuntimeMaintenanceActionKind.None;
    }

    private static string BuildProjectionSummary(
        RuntimeArtifactFamilyPolicy family,
        RuntimeArtifactObservation observation,
        RuntimeMaintenanceActionKind recommendedAction)
    {
        var action = recommendedAction == RuntimeMaintenanceActionKind.None
            ? "observe"
            : recommendedAction.ToString().ToLowerInvariant();
        return $"{family.DisplayName}: files={observation.FileCount}, bytes={observation.TotalBytes}, overdue={observation.RetentionOverdueCount}, hot_window_excess={observation.HotWindowExcessCount}, action={action}.";
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, Path.GetFullPath(path)).Replace('\\', '/');
    }

    private static bool ShouldIncludeFileInFamily(RuntimeArtifactFamilyPolicy family, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return family.FamilyId switch
        {
            "planning_runtime_history" => !IsTopLevelPlanningDraftSpill(normalized),
            "planning_draft_residue" => IsTopLevelPlanningDraftSpill(normalized),
            "platform_live_state" => !IsTemporaryAtomicWriteSpill(normalized),
            "platform_provider_live_state" => !IsTemporaryAtomicWriteSpill(normalized),
            "incident_audit_archive" => !IsTemporaryAtomicWriteSpill(normalized),
            "ephemeral_runtime_residue" => ShouldIncludeEphemeralResidue(normalized),
            "sustainability_projection" => !normalized.Contains("/runtime/sustainability/archive/", StringComparison.OrdinalIgnoreCase)
                                           && !normalized.EndsWith("/runtime/sustainability/archive", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private static bool IsTopLevelPlanningDraftSpill(string relativePath)
    {
        return relativePath.StartsWith(".ai/runtime/planning/card-drafts/CARD-", StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(".ai/runtime/planning/taskgraph-drafts/TG-CARD-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIncludeEphemeralResidue(string normalizedPath)
    {
        if (normalizedPath.Contains("/_quarantine/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith("/_quarantine", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedPath.StartsWith(".carves-platform/runtime-state/", StringComparison.OrdinalIgnoreCase))
        {
            return IsTemporaryAtomicWriteSpill(normalizedPath);
        }

        return true;
    }

    private static bool IsTemporaryAtomicWriteSpill(string normalizedPath)
    {
        return normalizedPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<SustainabilityAuditFinding> ScanCodeGraphResidueFindings()
    {
        var codeGraphRoot = Path.Combine(paths.AiRoot, "codegraph");
        if (!Directory.Exists(codeGraphRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(codeGraphRoot, "*", SearchOption.AllDirectories)
            .Select(file => ToRepoRelative(file))
            .Where(relativePath => DerivedForbiddenMarkers.Any(marker => relativePath.Replace('\\', '/').ToLowerInvariant().Contains(marker)))
            .Select(relativePath => new SustainabilityAuditFinding
            {
                Category = "derived_truth_pollution",
                Severity = "error",
                FamilyId = "codegraph_derived",
                Path = relativePath,
                Message = "CodeGraph storage contains generated/build residue that should stay outside derived truth.",
                RecommendedAction = RuntimeMaintenanceActionKind.RebuildDerived,
            })
            .ToArray();
    }

    private sealed record RuntimeArtifactObservation(
        int FileCount,
        long TotalBytes,
        int? OldestItemAgeDays,
        int RetentionOverdueCount,
        int ReadPathPressureCount,
        int HotWindowExcessCount,
        bool OverFileBudget,
        bool OverByteBudget,
        bool WithinBudget,
        IReadOnlyList<ResolvedArtifactFile> Files);

    private sealed record ResolvedArtifactFile(
        string RelativePath,
        long Length,
        DateTime LastWriteUtc);
}
