using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class OperationalHistoryCompactionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly RuntimeArtifactCatalogService catalogService;

    public OperationalHistoryCompactionService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        catalogService = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig);
    }

    public OperationalHistoryCompactionReport Compact()
    {
        var catalog = catalogService.LoadOrBuild();
        var archiveRoot = GetArchiveRoot(paths);
        Directory.CreateDirectory(archiveRoot);

        var existingIndex = LoadArchiveIndex();
        var entries = existingIndex.Entries.ToList();
        var actions = new List<string>();
        var familyReports = new List<OperationalHistoryCompactionFamily>();

        foreach (var family in catalog.Families.Where(item => item.CompactEligible))
        {
            var result = CompactFamily(family, archiveRoot);
            familyReports.Add(result.Family);
            actions.AddRange(result.Actions);
            entries.AddRange(result.Entries);
        }

        var updatedIndex = new OperationalHistoryArchiveIndex
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            Entries = entries
                .OrderByDescending(entry => entry.ArchivedAt)
                .ToArray(),
        };

        File.WriteAllText(GetArchiveIndexPath(paths), JsonSerializer.Serialize(updatedIndex, JsonOptions));

        var report = new OperationalHistoryCompactionReport
        {
            ArchiveRoot = ToRepoRelative(archiveRoot),
            ArchivedFileCount = familyReports.Sum(item => item.ArchivedFileCount),
            PreservedHotFileCount = familyReports.Sum(item => item.PreservedHotFileCount),
            Families = familyReports.ToArray(),
            ArchivedEntries = updatedIndex.Entries
                .OrderByDescending(entry => entry.ArchivedAt)
                .Take(100)
                .ToArray(),
            Actions = actions.ToArray(),
            Summary = $"Archived {familyReports.Sum(item => item.ArchivedFileCount)} operational-history file(s) and preserved {familyReports.Sum(item => item.PreservedHotFileCount)} hot-window file(s).",
        };

        File.WriteAllText(GetLatestReportPath(paths), JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public OperationalHistoryCompactionReport? TryLoadLatest()
    {
        var path = GetLatestReportPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OperationalHistoryCompactionReport>(File.ReadAllText(path), JsonOptions);
    }

    public OperationalHistoryArchiveIndex LoadArchiveIndex()
    {
        var path = GetArchiveIndexPath(paths);
        if (!File.Exists(path))
        {
            return new OperationalHistoryArchiveIndex();
        }

        return JsonSerializer.Deserialize<OperationalHistoryArchiveIndex>(File.ReadAllText(path), JsonOptions)
               ?? new OperationalHistoryArchiveIndex();
    }

    public static string GetArchiveRoot(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "archive");
    }

    public static string GetArchiveIndexPath(ControlPlanePaths paths)
    {
        return Path.Combine(GetArchiveRoot(paths), "index.json");
    }

    public static string GetLatestReportPath(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "compaction.json");
    }

    private FamilyCompactionResult CompactFamily(RuntimeArtifactFamilyPolicy family, string archiveRoot)
    {
        var files = ResolveCandidateFiles(family)
            .OrderByDescending(file => file.LastWriteUtc)
            .ThenBy(file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var keepCount = Math.Max(0, family.Budget.HotWindowCount ?? 0);
        var maxAgeDays = family.Budget.MaxAgeDays;
        var preservedHot = files
            .Select((file, index) => new { File = file, Index = index })
            .Where(item => item.Index < keepCount)
            .Where(item => IsWithinAgeWindow(item.File, maxAgeDays, now))
            .Select(item => item.File)
            .ToArray();
        var preservedPaths = preservedHot
            .Select(item => item.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var archiveCandidates = files
            .Where(file => !preservedPaths.Contains(file.SourcePath))
            .ToArray();
        var entries = new List<OperationalHistoryArchiveEntry>();
        var actions = new List<string>();

        foreach (var candidate in archiveCandidates)
        {
            var targetPath = BuildArchivePath(archiveRoot, family.FamilyId, candidate.SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(candidate.SourcePath, targetPath);
            var entry = new OperationalHistoryArchiveEntry
            {
                EntryId = BuildArchiveEntryId(family.FamilyId, candidate.SourcePath),
                FamilyId = family.FamilyId,
                OriginalPath = ToRepoRelative(candidate.SourcePath),
                ArchivedPath = ToRepoRelative(targetPath),
                ArchivedAt = DateTimeOffset.UtcNow,
                SourceLastWriteAt = candidate.LastWriteUtc,
                Summary = BuildArchiveSummary(family.FamilyId, candidate.SourcePath),
            };
            entries.Add(entry);
            actions.Add($"Archived '{entry.OriginalPath}' to '{entry.ArchivedPath}'.");
        }

        return new FamilyCompactionResult(
            new OperationalHistoryCompactionFamily
            {
                FamilyId = family.FamilyId,
                ArchivedFileCount = archiveCandidates.Length,
                PreservedHotFileCount = preservedHot.Length,
                ArchiveEntryCount = entries.Count,
                Summary = $"{family.DisplayName}: archived {archiveCandidates.Length}, preserved hot window {preservedHot.Length}.",
            },
            entries,
            actions);
    }

    private IReadOnlyList<CompactionCandidateFile> ResolveCandidateFiles(RuntimeArtifactFamilyPolicy family)
    {
        return family.FamilyId switch
        {
            "validation_trace_history" => ResolveFlatDirectory(Path.Combine(paths.AiRoot, "validation", "traces")),
            "execution_run_detail_history" => ResolveNestedRunDirectory(Path.Combine(paths.RuntimeRoot, "runs")),
            "execution_run_report_history" => ResolveNestedRunDirectory(Path.Combine(paths.RuntimeRoot, "run-reports")),
            "planning_runtime_history" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            "execution_surface_history" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            "context_pack_projection" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            "execution_packet_mirror" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            "runtime_failure_detail_history" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            "worker_execution_artifact_history" => ResolveRecursiveRoots(family.FamilyId, family.Roots),
            _ => [],
        };
    }

    private static IReadOnlyList<CompactionCandidateFile> ResolveFlatDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)
            .Select(file => new FileInfo(file))
            .Select(file => new CompactionCandidateFile(file.FullName, file.LastWriteTimeUtc))
            .ToArray();
    }

    private static IReadOnlyList<CompactionCandidateFile> ResolveNestedRunDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        return Directory.EnumerateDirectories(path)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            .Select(file => new FileInfo(file))
            .Select(file => new CompactionCandidateFile(file.FullName, file.LastWriteTimeUtc))
            .ToArray();
    }

    private IReadOnlyList<CompactionCandidateFile> ResolveRecursiveRoots(string familyId, IEnumerable<string> roots)
    {
        var files = new List<CompactionCandidateFile>();
        foreach (var root in roots)
        {
            var path = Path.Combine(repoRoot, root.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(path))
            {
                continue;
            }

            files.AddRange(
                Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Where(file => ShouldIncludeFileForFamily(familyId, ToRepoRelative(file)))
                    .Select(file => new FileInfo(file))
                    .Select(file => new CompactionCandidateFile(file.FullName, file.LastWriteTimeUtc)));
        }

        return files;
    }

    private string BuildArchivePath(string archiveRoot, string familyId, string sourcePath)
    {
        var relativeSourcePath = ToRepoRelative(sourcePath);
        return Path.Combine(archiveRoot, familyId, relativeSourcePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string BuildArchiveEntryId(string familyId, string sourcePath)
    {
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes($"{familyId}|{sourcePath}"))).ToLowerInvariant();
        return $"archive-{familyId}-{hash[..12]}";
    }

    private static string BuildArchiveSummary(string familyId, string sourcePath)
    {
        return familyId switch
        {
            "validation_trace_history" => $"Archived validation trace '{Path.GetFileNameWithoutExtension(sourcePath)}'.",
            "execution_run_detail_history" => $"Archived execution run '{Path.GetFileNameWithoutExtension(sourcePath)}' for task '{Path.GetFileName(Path.GetDirectoryName(sourcePath) ?? string.Empty)}'.",
            "execution_run_report_history" => $"Archived execution run report '{Path.GetFileNameWithoutExtension(sourcePath)}' for task '{Path.GetFileName(Path.GetDirectoryName(sourcePath) ?? string.Empty)}'.",
            "planning_runtime_history" => $"Archived planning runtime artifact '{Path.GetFileName(sourcePath)}' from '{Path.GetDirectoryName(sourcePath) ?? string.Empty}'.",
            "execution_surface_history" => $"Archived execution surface artifact '{Path.GetFileName(sourcePath)}' from '{Path.GetDirectoryName(sourcePath) ?? string.Empty}'.",
            "context_pack_projection" => $"Archived context-pack projection '{Path.GetFileName(sourcePath)}' from '{Path.GetDirectoryName(sourcePath) ?? string.Empty}'.",
            "execution_packet_mirror" => $"Archived execution packet mirror '{Path.GetFileName(sourcePath)}'.",
            "runtime_failure_detail_history" => $"Archived runtime failure detail '{Path.GetFileName(sourcePath)}' from '{Path.GetDirectoryName(sourcePath) ?? string.Empty}'.",
            "worker_execution_artifact_history" => $"Archived worker-facing artifact '{Path.GetFileName(sourcePath)}' from '{Path.GetDirectoryName(sourcePath) ?? string.Empty}'.",
            _ => $"Archived operational history '{Path.GetFileName(sourcePath)}'.",
        };
    }

    private static bool IsWithinAgeWindow(CompactionCandidateFile file, int? maxAgeDays, DateTimeOffset now)
    {
        if (maxAgeDays is null)
        {
            return true;
        }

        return (now - file.LastWriteUtc).TotalDays <= maxAgeDays.Value;
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, Path.GetFullPath(path)).Replace('\\', '/');
    }

    private static bool ShouldIncludeFileForFamily(string familyId, string relativePath)
    {
        if (!string.Equals(familyId, "planning_runtime_history", StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = relativePath.Replace('\\', '/');
        return !normalized.StartsWith(".ai/runtime/planning/card-drafts/CARD-", StringComparison.OrdinalIgnoreCase)
               && !normalized.StartsWith(".ai/runtime/planning/taskgraph-drafts/TG-CARD-", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CompactionCandidateFile(
        string SourcePath,
        DateTime LastWriteUtc);

    private sealed record FamilyCompactionResult(
        OperationalHistoryCompactionFamily Family,
        IReadOnlyList<OperationalHistoryArchiveEntry> Entries,
        IReadOnlyList<string> Actions);
}
