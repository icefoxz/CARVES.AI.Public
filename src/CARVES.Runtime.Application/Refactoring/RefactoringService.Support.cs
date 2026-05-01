using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Refactoring;

public sealed partial class RefactoringService
{
    private static readonly TaskTypePolicy TaskTypePolicy = TaskTypePolicy.Default;
    private static readonly Regex StableIdSanitizer = new("[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex StableIdHyphenCollapser = new("-{2,}", RegexOptions.Compiled);

    private static string BuildFingerprint(RefactoringFinding finding)
    {
        return $"{finding.Kind}|{finding.Path}";
    }

    private static string MapPriority(RefactoringFinding finding)
    {
        return string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)
            ? "P2"
            : "P3";
    }

    private static RefactoringBacklogItem NormalizeExistingItem(RefactoringBacklogItem item, string fingerprint, DateTimeOffset fallbackTimestamp)
    {
        var path = string.IsNullOrWhiteSpace(item.Path) ? "unknown" : item.Path;
        return new RefactoringBacklogItem
        {
            ItemId = string.IsNullOrWhiteSpace(item.ItemId) ? BuildStableId("RB", fingerprint, path) : item.ItemId,
            Fingerprint = fingerprint,
            Kind = item.Kind,
            Path = path,
            Reason = item.Reason,
            Severity = string.IsNullOrWhiteSpace(item.Severity) ? "warning" : item.Severity,
            Priority = string.IsNullOrWhiteSpace(item.Priority) ? "P3" : item.Priority,
            Status = item.Status,
            SuggestedTaskId = item.SuggestedTaskId,
            FirstDetectedAt = item.FirstDetectedAt == default ? fallbackTimestamp : item.FirstDetectedAt,
            LastDetectedAt = item.LastDetectedAt == default ? fallbackTimestamp : item.LastDetectedAt,
            ResolvedAt = item.ResolvedAt,
            Metrics = item.Metrics,
        };
    }

    private static bool HasHigherPriorityActiveWork(DomainTaskGraph graph)
    {
        return graph.ListTasks().Any(task =>
            task.Status is DomainTaskStatus.Pending or DomainTaskStatus.Running or DomainTaskStatus.Testing or DomainTaskStatus.Review &&
            task.Priority is "P0" or "P1" &&
            !string.Equals(task.Source, "REFACTORING_BACKLOG", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStableId(string prefix, string fingerprint, string path)
    {
        var fileStem = Path.GetFileNameWithoutExtension(path)
            .Replace('.', '-')
            .Replace('_', '-')
            .ToLowerInvariant();
        fileStem = StableIdSanitizer.Replace(fileStem, "-");
        fileStem = StableIdHyphenCollapser.Replace(fileStem, "-").Trim('-');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant()[..8];
        return $"{prefix}-{fileStem}-{hash}";
    }

    private static int PriorityWeight(string priority)
    {
        return priority switch
        {
            "P0" => 0,
            "P1" => 1,
            "P2" => 2,
            "P3" => 3,
            _ => 99,
        };
    }
}
