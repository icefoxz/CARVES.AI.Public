namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeFileGranularityAuditService
{
    private const int TinyFileLineThreshold = 25;
    private const int SmallFileLineThreshold = 50;
    private const int LargeFileLineThreshold = 400;
    private const int HugeFileLineThreshold = 800;
    private const int TinyClusterMinimumFileCount = 5;
    private const int PartialClusterMinimumFileCount = 3;
    private const int MaxProjectedEntries = 20;
    private const int MaxCleanupCandidateCount = 12;
    private const int MaxSelectedCleanupBatchSize = 3;
    private static readonly string[] ScanRoots = ["src", "tests"];
    private static readonly string[] ExcludedDirectoryNames = ["bin", "obj", ".git", ".vs"];

    private readonly string repoRoot;

    public RuntimeFileGranularityAuditService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeFileGranularityAuditSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var files = LoadSourceFiles(errors, warnings);
        var projectRollups = BuildProjectRollups(files).ToArray();
        var directoryPressure = BuildDirectoryPressure(files).ToArray();
        var tinyClusters = BuildTinyClusters(files).ToArray();
        var partialFamilyClusters = BuildPartialFamilyClusters(files).ToArray();
        var counts = BuildCounts(files, tinyClusters, partialFamilyClusters, directoryPressure);
        var findings = BuildFindings(counts, tinyClusters, partialFamilyClusters).ToArray();
        var isValid = errors.Count == 0;
        var overallPosture = ClassifyOverallPosture(isValid, counts, findings);
        var cleanupCandidates = BuildCleanupCandidates(files, directoryPressure, tinyClusters, partialFamilyClusters).ToArray();
        var cleanupSelection = BuildCleanupSelection(cleanupCandidates);

        return new RuntimeFileGranularityAuditSurface
        {
            RepoRoot = repoRoot,
            IsValid = isValid,
            OverallPosture = overallPosture,
            ScanRoots = ScanRoots,
            ExcludedDirectoryNames = ExcludedDirectoryNames,
            Thresholds = new RuntimeFileGranularityThresholdsSurface
            {
                TinyFileLineThreshold = TinyFileLineThreshold,
                SmallFileLineThreshold = SmallFileLineThreshold,
                LargeFileLineThreshold = LargeFileLineThreshold,
                HugeFileLineThreshold = HugeFileLineThreshold,
                TinyClusterMinimumFileCount = TinyClusterMinimumFileCount,
                PartialClusterMinimumFileCount = PartialClusterMinimumFileCount,
            },
            Counts = counts,
            ProjectRollups = projectRollups,
            DirectoryPressure = directoryPressure.Take(MaxProjectedEntries).ToArray(),
            TinyClusters = tinyClusters.Take(MaxProjectedEntries).ToArray(),
            PartialFamilyClusters = partialFamilyClusters.Take(MaxProjectedEntries).ToArray(),
            LargestFiles = files
                .OrderByDescending(static file => file.PhysicalLineCount)
                .ThenBy(static file => file.Path, StringComparer.Ordinal)
                .Take(MaxProjectedEntries)
                .Select(ToSurface)
                .ToArray(),
            SmallestFiles = files
                .OrderBy(static file => file.PhysicalLineCount)
                .ThenBy(static file => file.Path, StringComparer.Ordinal)
                .Take(MaxProjectedEntries)
                .Select(ToSurface)
                .ToArray(),
            CleanupCandidates = cleanupCandidates,
            CleanupSelection = cleanupSelection,
            Findings = findings,
            Errors = errors,
            Warnings = warnings,
            Summary = BuildSummary(counts, overallPosture),
            RecommendedNextAction = BuildRecommendedNextAction(overallPosture),
            NonClaims =
            [
                "This audit is read-only and does not merge, split, move, delete, rewrite, stage, commit, or approve any file.",
                "Tiny files are not automatically bad; enums, marker interfaces, global usings, DTOs, and public contracts can be legitimate one-file units.",
                "Large files are not automatically bad; generated-like fixtures, host-contract tests, and dense orchestration code require human review before decomposition.",
                "Partial-family clusters are not treated as violations when the split preserves clear responsibility boundaries.",
                "Cleanup candidates are bounded review queues, not authorization to mechanically merge or split files.",
                "This surface measures C# file granularity under src/ and tests/ only; it is not a whole-repo documentation or artifact audit."
            ],
        };
    }

    private SourceFileGranularityRecord[] LoadSourceFiles(List<string> errors, List<string> warnings)
    {
        if (!Directory.Exists(repoRoot))
        {
            errors.Add($"Repo root '{repoRoot}' does not exist.");
            return [];
        }

        var records = new List<SourceFileGranularityRecord>();
        foreach (var scanRoot in ScanRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, scanRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                warnings.Add($"Scan root '{scanRoot}' does not exist.");
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldExclude(path))
                {
                    continue;
                }

                try
                {
                    var lines = File.ReadAllLines(path);
                    var relativePath = ToRepoRelative(path);
                    records.Add(new SourceFileGranularityRecord(
                        relativePath,
                        GetProjectRoot(relativePath),
                        GetDirectory(relativePath),
                        Path.GetFileNameWithoutExtension(path),
                        lines.Length,
                        lines.Count(static line => !string.IsNullOrWhiteSpace(line)),
                        ClassifyFile(lines.Length)));
                }
                catch (IOException ex)
                {
                    warnings.Add($"Unable to read '{ToRepoRelative(path)}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    warnings.Add($"Unable to read '{ToRepoRelative(path)}': {ex.Message}");
                }
            }
        }

        return records
            .OrderBy(static file => file.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static RuntimeFileGranularityCountsSurface BuildCounts(
        IReadOnlyCollection<SourceFileGranularityRecord> files,
        IReadOnlyCollection<RuntimeFileGranularityTinyClusterSurface> tinyClusters,
        IReadOnlyCollection<RuntimeFileGranularityPartialClusterSurface> partialFamilyClusters,
        IReadOnlyCollection<RuntimeFileGranularityDirectoryRollupSurface> directoryPressure)
    {
        var totalFiles = files.Count;
        var totalLines = files.Sum(static file => file.PhysicalLineCount);
        var orderedLines = files.Select(static file => file.PhysicalLineCount).Order().ToArray();
        var tinyCount = files.Count(static file => file.PhysicalLineCount <= TinyFileLineThreshold);
        var smallCount = files.Count(static file => file.PhysicalLineCount > TinyFileLineThreshold && file.PhysicalLineCount <= SmallFileLineThreshold);
        var largeCount = files.Count(static file => file.PhysicalLineCount >= LargeFileLineThreshold && file.PhysicalLineCount < HugeFileLineThreshold);
        var hugeCount = files.Count(static file => file.PhysicalLineCount >= HugeFileLineThreshold);

        return new RuntimeFileGranularityCountsSurface
        {
            TotalFileCount = totalFiles,
            SourceFileCount = files.Count(static file => file.Path.StartsWith("src/", StringComparison.Ordinal)),
            TestFileCount = files.Count(static file => file.Path.StartsWith("tests/", StringComparison.Ordinal)),
            TotalPhysicalLineCount = totalLines,
            TotalNonBlankLineCount = files.Sum(static file => file.NonBlankLineCount),
            AveragePhysicalLinesPerFile = Round(totalFiles == 0 ? 0 : (double)totalLines / totalFiles),
            MedianPhysicalLinesPerFile = Round(CalculateMedian(orderedLines)),
            TinyFileCount = tinyCount,
            SmallFileCount = smallCount,
            MediumFileCount = files.Count(static file => file.PhysicalLineCount > SmallFileLineThreshold && file.PhysicalLineCount < LargeFileLineThreshold),
            LargeFileCount = largeCount,
            HugeFileCount = hugeCount,
            TinyFileRatio = Round(totalFiles == 0 ? 0 : (double)tinyCount / totalFiles),
            SmallFileRatio = Round(totalFiles == 0 ? 0 : (double)(tinyCount + smallCount) / totalFiles),
            TinyClusterCount = tinyClusters.Count,
            PartialFamilyClusterCount = partialFamilyClusters.Count,
            DirectoryPressureCount = directoryPressure.Count,
        };
    }

    private static IEnumerable<RuntimeFileGranularityProjectRollupSurface> BuildProjectRollups(IEnumerable<SourceFileGranularityRecord> files)
    {
        return files
            .GroupBy(static file => file.ProjectRoot, StringComparer.Ordinal)
            .Select(static group =>
            {
                var items = group.ToArray();
                return new RuntimeFileGranularityProjectRollupSurface
                {
                    ProjectRoot = group.Key,
                    FileCount = items.Length,
                    TotalPhysicalLineCount = items.Sum(static file => file.PhysicalLineCount),
                    AveragePhysicalLinesPerFile = Round(items.Average(static file => file.PhysicalLineCount)),
                    TinyFileCount = items.Count(static file => file.PhysicalLineCount <= TinyFileLineThreshold),
                    SmallFileCount = items.Count(static file => file.PhysicalLineCount > TinyFileLineThreshold && file.PhysicalLineCount <= SmallFileLineThreshold),
                    LargeFileCount = items.Count(static file => file.PhysicalLineCount >= LargeFileLineThreshold && file.PhysicalLineCount < HugeFileLineThreshold),
                    HugeFileCount = items.Count(static file => file.PhysicalLineCount >= HugeFileLineThreshold),
                };
            })
            .OrderByDescending(static rollup => rollup.FileCount)
            .ThenBy(static rollup => rollup.ProjectRoot, StringComparer.Ordinal);
    }

    private static IEnumerable<RuntimeFileGranularityDirectoryRollupSurface> BuildDirectoryPressure(IEnumerable<SourceFileGranularityRecord> files)
    {
        return files
            .GroupBy(static file => file.DirectoryPath, StringComparer.Ordinal)
            .Select(static group =>
            {
                var items = group.ToArray();
                var tinyCount = items.Count(static file => file.PhysicalLineCount <= TinyFileLineThreshold);
                var largeCount = items.Count(static file => file.PhysicalLineCount >= LargeFileLineThreshold && file.PhysicalLineCount < HugeFileLineThreshold);
                var hugeCount = items.Count(static file => file.PhysicalLineCount >= HugeFileLineThreshold);
                return new RuntimeFileGranularityDirectoryRollupSurface
                {
                    DirectoryPath = group.Key,
                    FileCount = items.Length,
                    TotalPhysicalLineCount = items.Sum(static file => file.PhysicalLineCount),
                    AveragePhysicalLinesPerFile = Round(items.Average(static file => file.PhysicalLineCount)),
                    TinyFileCount = tinyCount,
                    SmallFileCount = items.Count(static file => file.PhysicalLineCount > TinyFileLineThreshold && file.PhysicalLineCount <= SmallFileLineThreshold),
                    LargeFileCount = largeCount,
                    HugeFileCount = hugeCount,
                    PressureScore = tinyCount + (largeCount * 4) + (hugeCount * 8),
                    ExampleFiles = items.OrderBy(static file => file.PhysicalLineCount).ThenBy(static file => file.Path, StringComparer.Ordinal).Take(8).Select(static file => file.Path).ToArray(),
                };
            })
            .Where(static rollup => rollup.PressureScore > 0)
            .OrderByDescending(static rollup => rollup.PressureScore)
            .ThenByDescending(static rollup => rollup.FileCount)
            .ThenBy(static rollup => rollup.DirectoryPath, StringComparer.Ordinal);
    }

    private static IEnumerable<RuntimeFileGranularityTinyClusterSurface> BuildTinyClusters(IEnumerable<SourceFileGranularityRecord> files)
    {
        return files
            .GroupBy(static file => file.DirectoryPath, StringComparer.Ordinal)
            .Select(static group =>
            {
                var items = group.ToArray();
                var tinyFiles = items
                    .Where(static file => file.PhysicalLineCount <= TinyFileLineThreshold)
                    .OrderBy(static file => file.PhysicalLineCount)
                    .ThenBy(static file => file.Path, StringComparer.Ordinal)
                    .ToArray();
                return new RuntimeFileGranularityTinyClusterSurface
                {
                    ClusterId = $"tiny:{group.Key}",
                    DirectoryPath = group.Key,
                    TinyFileCount = tinyFiles.Length,
                    SmallFileCount = items.Count(static file => file.PhysicalLineCount <= SmallFileLineThreshold),
                    AveragePhysicalLinesPerTinyFile = tinyFiles.Length == 0 ? 0 : Round(tinyFiles.Average(static file => file.PhysicalLineCount)),
                    ReviewPosture = ClassifyTinyClusterPosture(group.Key),
                    RecommendedAction = "Review as a cohesive local family before merging anything; consolidate only when files share lifecycle, ownership, and validation surface.",
                    ExampleFiles = tinyFiles.Take(8).Select(static file => file.Path).ToArray(),
                };
            })
            .Where(static cluster => cluster.TinyFileCount >= TinyClusterMinimumFileCount)
            .OrderByDescending(static cluster => cluster.TinyFileCount)
            .ThenBy(static cluster => cluster.DirectoryPath, StringComparer.Ordinal);
    }

    private static IEnumerable<RuntimeFileGranularityPartialClusterSurface> BuildPartialFamilyClusters(IEnumerable<SourceFileGranularityRecord> files)
    {
        return files
            .Select(static file => new { File = file, FamilyName = GetPartialFamilyName(file.FileStem) })
            .Where(static item => !string.IsNullOrWhiteSpace(item.FamilyName))
            .GroupBy(static item => $"{item.File.DirectoryPath}/{item.FamilyName}", StringComparer.Ordinal)
            .Select(static group =>
            {
                var items = group.Select(static item => item.File).ToArray();
                var familyName = GetPartialFamilyName(items[0].FileStem);
                return new RuntimeFileGranularityPartialClusterSurface
                {
                    ClusterId = $"partial:{group.Key}",
                    DirectoryPath = items[0].DirectoryPath,
                    FamilyName = familyName,
                    FileCount = items.Length,
                    TotalPhysicalLineCount = items.Sum(static file => file.PhysicalLineCount),
                    AveragePhysicalLinesPerFile = Round(items.Average(static file => file.PhysicalLineCount)),
                    TinyFileCount = items.Count(static file => file.PhysicalLineCount <= TinyFileLineThreshold),
                    ReviewPosture = "partial_family_review",
                    RecommendedAction = "Keep the split when each file owns a clear behavior slice; consolidate tiny slices that only add navigation overhead.",
                    Files = items.OrderBy(static file => file.Path, StringComparer.Ordinal).Select(static file => file.Path).ToArray(),
                };
            })
            .Where(static cluster => cluster.FileCount >= PartialClusterMinimumFileCount)
            .OrderByDescending(static cluster => cluster.FileCount)
            .ThenByDescending(static cluster => cluster.TotalPhysicalLineCount)
            .ThenBy(static cluster => cluster.ClusterId, StringComparer.Ordinal);
    }

    private static IEnumerable<string> BuildFindings(
        RuntimeFileGranularityCountsSurface counts,
        IReadOnlyCollection<RuntimeFileGranularityTinyClusterSurface> tinyClusters,
        IReadOnlyCollection<RuntimeFileGranularityPartialClusterSurface> partialFamilyClusters)
    {
        if (counts.TotalFileCount == 0)
        {
            yield return "no_csharp_files_found";
            yield break;
        }

        if (counts.TinyFileRatio >= 0.20)
        {
            yield return $"tiny_file_pressure: tiny_files={counts.TinyFileCount}, total_files={counts.TotalFileCount}, ratio={counts.TinyFileRatio}";
        }

        if (counts.SmallFileRatio >= 0.35)
        {
            yield return $"small_file_pressure: small_or_tiny_files={counts.TinyFileCount + counts.SmallFileCount}, total_files={counts.TotalFileCount}, ratio={counts.SmallFileRatio}";
        }

        if (counts.HugeFileCount > 0)
        {
            yield return $"huge_file_pressure: huge_files={counts.HugeFileCount}, threshold_lines={HugeFileLineThreshold}";
        }

        if (tinyClusters.Count > 0)
        {
            yield return $"tiny_cluster_pressure: clusters={tinyClusters.Count}, minimum_files={TinyClusterMinimumFileCount}";
        }

        if (partialFamilyClusters.Count > 0)
        {
            yield return $"partial_family_cluster_visibility: clusters={partialFamilyClusters.Count}, minimum_files={PartialClusterMinimumFileCount}";
        }
    }

    private static RuntimeFileGranularityFileSurface ToSurface(SourceFileGranularityRecord file)
    {
        return new RuntimeFileGranularityFileSurface
        {
            Path = file.Path,
            PhysicalLineCount = file.PhysicalLineCount,
            NonBlankLineCount = file.NonBlankLineCount,
            FileClass = file.FileClass,
            SuggestedReviewPosture = ClassifyFileReviewPosture(file),
        };
    }

    private static IEnumerable<RuntimeFileGranularityCleanupCandidateSurface> BuildCleanupCandidates(
        IReadOnlyCollection<SourceFileGranularityRecord> files,
        IReadOnlyCollection<RuntimeFileGranularityDirectoryRollupSurface> directoryPressure,
        IReadOnlyCollection<RuntimeFileGranularityTinyClusterSurface> tinyClusters,
        IReadOnlyCollection<RuntimeFileGranularityPartialClusterSurface> partialFamilyClusters)
    {
        var candidates = new List<RuntimeFileGranularityCleanupCandidateSurface>();
        candidates.AddRange(directoryPressure
            .Where(static directory => directory.PressureScore >= 50)
            .Take(4)
            .Select(static directory => BuildDirectoryPressureCandidate(directory)));
        candidates.AddRange(files
            .Where(static file => file.PhysicalLineCount >= HugeFileLineThreshold)
            .OrderByDescending(static file => file.PhysicalLineCount)
            .ThenBy(static file => file.Path, StringComparer.Ordinal)
            .Take(4)
            .Select(static file => BuildHugeFileCandidate(file)));
        candidates.AddRange(tinyClusters
            .Take(4)
            .Select(static cluster => BuildTinyClusterCandidate(cluster)));
        candidates.AddRange(partialFamilyClusters
            .Where(static cluster => cluster.FileCount >= 8 || cluster.TinyFileCount >= 3)
            .Take(6)
            .Select(static cluster => BuildPartialFamilyCandidate(cluster)));

        return candidates
            .OrderByDescending(static candidate => candidate.PressureScore)
            .ThenBy(static candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Take(MaxCleanupCandidateCount);
    }

    private static RuntimeFileGranularityCleanupSelectionSurface BuildCleanupSelection(
        IReadOnlyList<RuntimeFileGranularityCleanupCandidateSurface> candidates)
    {
        var selected = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Eligibility = ClassifySelectionEligibility(candidate),
            })
            .Where(item => item.Eligibility.IsEligible)
            .OrderBy(static item => item.Eligibility.RiskRank)
            .ThenByDescending(static item => item.Candidate.PressureScore)
            .ThenBy(static item => item.Candidate.CandidateId, StringComparer.Ordinal)
            .Take(MaxSelectedCleanupBatchSize)
            .Select((item, index) => new RuntimeFileGranularitySelectedCleanupCandidateSurface
            {
                SelectionRank = index + 1,
                CandidateId = item.Candidate.CandidateId,
                CandidateType = item.Candidate.CandidateType,
                Priority = item.Candidate.Priority,
                RiskClass = item.Eligibility.RiskClass,
                ScopePath = item.Candidate.ScopePath,
                PressureScore = item.Candidate.PressureScore,
                SelectionReason = item.Eligibility.SelectionReason,
                RecommendedAction = item.Candidate.RecommendedAction,
                ValidationHint = item.Candidate.ValidationHint,
                Files = item.Candidate.Files,
            })
            .ToArray();
        var selectedIds = selected
            .Select(static item => item.CandidateId)
            .ToHashSet(StringComparer.Ordinal);

        return new RuntimeFileGranularityCleanupSelectionSurface
        {
            MaxBatchSize = MaxSelectedCleanupBatchSize,
            CandidateCount = candidates.Count,
            SelectedCandidateCount = selected.Length,
            DeferredCandidateCount = candidates.Count(candidate => !selectedIds.Contains(candidate.CandidateId)),
            EligibilityRules =
            [
                "Prefer tests-only huge-file decomposition candidates because they can reduce navigation cost without changing Runtime production behavior.",
                "Allow projection/formatter partial-family budget reviews only after tests-only candidates, and only as review candidates rather than merge instructions.",
                "Keep the first cleanup batch at three or fewer candidates."
            ],
            DeferralRules =
            [
                "Defer directory-wide pressure candidates until a narrower family inside that directory is selected.",
                "Defer core Runtime src/service candidates when the candidate would touch execution truth, task truth, safety gates, or broad operator surfaces without a specific card.",
                "Defer tiny contract/model clusters unless human review confirms the files share lifecycle and ownership."
            ],
            SelectedCandidates = selected,
            DeferredCandidateIds = candidates
                .Where(candidate => !selectedIds.Contains(candidate.CandidateId))
                .Select(static candidate => candidate.CandidateId)
                .ToArray(),
            NonClaims =
            [
                "Selection is not approval to edit; each selected candidate still needs a bounded cleanup card or an explicit operator instruction.",
                "Selection does not claim semantic risk is zero.",
                "Deferred candidates remain visible and are not treated as resolved."
            ],
        };
    }

    private static RuntimeFileGranularityCleanupCandidateSurface BuildDirectoryPressureCandidate(RuntimeFileGranularityDirectoryRollupSurface directory)
    {
        return new RuntimeFileGranularityCleanupCandidateSurface
        {
            CandidateId = $"directory-pressure:{NormalizeId(directory.DirectoryPath)}",
            CandidateType = "directory_pressure_review",
            Priority = directory.PressureScore >= 100 ? "high" : "medium",
            ScopePath = directory.DirectoryPath,
            PressureScore = directory.PressureScore,
            Rationale = $"Directory has mixed granularity pressure: files={directory.FileCount}, tiny={directory.TinyFileCount}, large={directory.LargeFileCount}, huge={directory.HugeFileCount}.",
            RecommendedAction = "Open a bounded cleanup card for one cohesive family inside this directory, not for the whole directory at once.",
            ValidationHint = "Run the owning project tests plus any host-contract tests touched by the selected family.",
            Files = directory.ExampleFiles,
            NonClaims = ["Directory pressure does not mean every file in the directory should be moved or merged."],
        };
    }

    private static RuntimeFileGranularityCleanupCandidateSurface BuildHugeFileCandidate(SourceFileGranularityRecord file)
    {
        return new RuntimeFileGranularityCleanupCandidateSurface
        {
            CandidateId = $"huge-file:{NormalizeId(file.Path)}",
            CandidateType = "huge_file_decomposition_review",
            Priority = file.PhysicalLineCount >= 1200 ? "high" : "medium",
            ScopePath = file.Path,
            PressureScore = file.PhysicalLineCount,
            Rationale = $"File has {file.PhysicalLineCount} physical lines, above the huge-file threshold of {HugeFileLineThreshold}.",
            RecommendedAction = "Review for one behavior-preserving extraction only if a clear responsibility boundary and local validation path exist.",
            ValidationHint = "Run targeted tests for this file's owning module; for test files, keep assertions stable and avoid fixture churn.",
            Files = [file.Path],
            NonClaims = ["Huge file pressure does not prove the file is incorrectly structured."],
        };
    }

    private static RuntimeFileGranularityCleanupCandidateSurface BuildTinyClusterCandidate(RuntimeFileGranularityTinyClusterSurface cluster)
    {
        var score = cluster.TinyFileCount * 10;
        return new RuntimeFileGranularityCleanupCandidateSurface
        {
            CandidateId = $"tiny-cluster:{NormalizeId(cluster.DirectoryPath)}",
            CandidateType = "tiny_cluster_consolidation_review",
            Priority = cluster.TinyFileCount >= 15 ? "high" : "medium",
            ScopePath = cluster.DirectoryPath,
            PressureScore = score,
            Rationale = $"Directory has {cluster.TinyFileCount} tiny files; average tiny file length is {cluster.AveragePhysicalLinesPerTinyFile}.",
            RecommendedAction = cluster.RecommendedAction,
            ValidationHint = "Consolidate only files with shared ownership and shared change cadence; rerun compile and owning tests.",
            Files = cluster.ExampleFiles,
            NonClaims = ["Tiny cluster pressure does not apply to legitimate public contract/model files without human review."],
        };
    }

    private static RuntimeFileGranularityCleanupCandidateSurface BuildPartialFamilyCandidate(RuntimeFileGranularityPartialClusterSurface cluster)
    {
        var score = cluster.FileCount * 8 + cluster.TinyFileCount * 4;
        return new RuntimeFileGranularityCleanupCandidateSurface
        {
            CandidateId = $"partial-family:{NormalizeId(cluster.DirectoryPath)}:{NormalizeId(cluster.FamilyName)}",
            CandidateType = "partial_family_budget_review",
            Priority = cluster.FileCount >= 20 ? "high" : "medium",
            ScopePath = $"{cluster.DirectoryPath}/{cluster.FamilyName}.*.cs",
            PressureScore = score,
            Rationale = $"Partial family has {cluster.FileCount} files and {cluster.TotalPhysicalLineCount} physical lines; tiny_slices={cluster.TinyFileCount}.",
            RecommendedAction = "Review whether the partial split still maps to durable responsibilities; merge only tiny slices that lack independent ownership.",
            ValidationHint = "Run compile plus tests covering the partial family; avoid cross-family moves in the same card.",
            Files = cluster.Files.Take(12).ToArray(),
            NonClaims = ["Partial-family size can be intentional; this candidate is a budget review, not a merge instruction."],
        };
    }

    private static CleanupSelectionEligibility ClassifySelectionEligibility(RuntimeFileGranularityCleanupCandidateSurface candidate)
    {
        if (candidate.ScopePath.StartsWith("tests/", StringComparison.Ordinal)
            && string.Equals(candidate.CandidateType, "huge_file_decomposition_review", StringComparison.Ordinal))
        {
            return new CleanupSelectionEligibility(
                true,
                0,
                "lower_risk_test_scope",
                "Tests-only huge-file decomposition can reduce navigation pressure without touching Runtime production behavior.");
        }

        if (candidate.ScopePath.StartsWith("src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.", StringComparison.Ordinal)
            && string.Equals(candidate.CandidateType, "partial_family_budget_review", StringComparison.Ordinal))
        {
            return new CleanupSelectionEligibility(
                true,
                1,
                "medium_risk_projection_formatter_scope",
                "Formatter partial-family review is projection-oriented and can be bounded after tests-only candidates.");
        }

        return new CleanupSelectionEligibility(
            false,
            99,
            "deferred_requires_narrower_card",
            "Candidate is deferred because it is too broad, production-core, contract/model-heavy, or requires a more specific governed card.");
    }

    private static string ClassifyOverallPosture(bool isValid, RuntimeFileGranularityCountsSurface counts, IReadOnlyCollection<string> findings)
    {
        if (!isValid)
        {
            return "file_granularity_audit_blocked";
        }

        if (counts.TotalFileCount == 0)
        {
            return "file_granularity_no_csharp_files";
        }

        return findings.Count == 0
            ? "file_granularity_balanced"
            : "file_granularity_pressure_observed";
    }

    private static string BuildSummary(RuntimeFileGranularityCountsSurface counts, string posture)
    {
        return $"File granularity audit scanned {counts.TotalFileCount} C# files and {counts.TotalPhysicalLineCount} physical lines; avg={counts.AveragePhysicalLinesPerFile}, median={counts.MedianPhysicalLinesPerFile}, tiny={counts.TinyFileCount}, large={counts.LargeFileCount}, huge={counts.HugeFileCount}, posture={posture}.";
    }

    private static string BuildRecommendedNextAction(string posture)
    {
        return posture switch
        {
            "file_granularity_audit_blocked" => "Restore readable src/ or tests/ roots before using this audit.",
            "file_granularity_pressure_observed" => "Use the projected tiny clusters, partial-family clusters, and largest files as a review queue; open bounded cleanup cards only for cohesive families with shared ownership and tests.",
            _ => "Keep this surface as a periodic maintenance readback; do not create cleanup work unless a concrete navigation, ownership, or validation cost appears.",
        };
    }

    private static string ClassifyFile(int lineCount)
    {
        if (lineCount <= TinyFileLineThreshold)
        {
            return "tiny";
        }

        if (lineCount <= SmallFileLineThreshold)
        {
            return "small";
        }

        if (lineCount >= HugeFileLineThreshold)
        {
            return "huge";
        }

        if (lineCount >= LargeFileLineThreshold)
        {
            return "large";
        }

        return "medium";
    }

    private static string ClassifyFileReviewPosture(SourceFileGranularityRecord file)
    {
        return file.FileClass switch
        {
            "tiny" when IsLikelyContractOrMarker(file.Path) => "likely_legitimate_contract_or_marker",
            "tiny" => "review_for_possible_family_consolidation",
            "huge" => "review_for_bounded_decomposition",
            "large" => "review_for_extraction_only_if_behavior_boundary_is_clear",
            _ => "no_immediate_granularity_action",
        };
    }

    private static string ClassifyTinyClusterPosture(string directoryPath)
    {
        if (directoryPath.Contains("/SurfaceModels", StringComparison.Ordinal)
            || directoryPath.Contains("/Domain/", StringComparison.Ordinal))
        {
            return "contract_or_model_cluster_review";
        }

        return directoryPath.StartsWith("tests/", StringComparison.Ordinal)
            ? "test_fixture_or_contract_cluster_review"
            : "implementation_cluster_review";
    }

    private static bool IsLikelyContractOrMarker(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith('I')
            || fileName.Contains("Dto", StringComparison.Ordinal)
            || fileName.Contains("Record", StringComparison.Ordinal)
            || fileName.Contains("Enum", StringComparison.Ordinal)
            || fileName.Contains("Status", StringComparison.Ordinal)
            || fileName.Contains("GlobalUsings", StringComparison.Ordinal);
    }

    private static string GetProjectRoot(string relativePath)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : relativePath;
    }

    private static string GetDirectory(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index < 0 ? "." : relativePath[..index];
    }

    private static string GetPartialFamilyName(string fileStem)
    {
        var dotIndex = fileStem.IndexOf('.');
        return dotIndex <= 0 ? string.Empty : fileStem[..dotIndex];
    }

    private bool ShouldExclude(string path)
    {
        var relative = ToRepoRelative(path);
        if (relative.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => ExcludedDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
    }

    private static double CalculateMedian(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle = values.Count / 2;
        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2.0;
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeId(string value)
    {
        var chars = value
            .Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();
        return string.Join(
            '-',
            new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record SourceFileGranularityRecord(
        string Path,
        string ProjectRoot,
        string DirectoryPath,
        string FileStem,
        int PhysicalLineCount,
        int NonBlankLineCount,
        string FileClass);

    private sealed record CleanupSelectionEligibility(
        bool IsEligible,
        int RiskRank,
        string RiskClass,
        string SelectionReason);
}
