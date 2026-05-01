using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class OperationalHistoryArchiveFollowUpQueueService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly OperationalHistoryArchiveReadinessService archiveReadinessService;

    public OperationalHistoryArchiveFollowUpQueueService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.paths = paths;
        archiveReadinessService = new OperationalHistoryArchiveReadinessService(repoRoot, paths, systemConfig);
    }

    public OperationalHistoryArchiveFollowUpQueue Build()
    {
        var readiness = archiveReadinessService.Build();
        var entries = readiness.PromotionRelevantEntries
            .Select(BuildEntry)
            .OrderBy(entry => entry.GroupId, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.ArchivedAt)
            .ThenBy(entry => entry.TaskId, StringComparer.Ordinal)
            .ToArray();
        var groups = entries
            .GroupBy(entry => entry.GroupId, StringComparer.Ordinal)
            .Select(group => BuildGroup(group.Key, group.ToArray()))
            .OrderByDescending(group => group.ItemCount)
            .ThenBy(group => group.GroupId, StringComparer.Ordinal)
            .ToArray();

        var queue = new OperationalHistoryArchiveFollowUpQueue
        {
            SourceArchiveReadinessReportId = readiness.ReportId,
            Groups = groups,
            Entries = entries,
            Summary = $"Projected {entries.Length} archive follow-up item(s) across {groups.Length} queue group(s).",
        };
        Persist(queue);
        return queue;
    }

    public OperationalHistoryArchiveFollowUpQueue? TryLoadLatest()
    {
        var path = GetLatestReportPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OperationalHistoryArchiveFollowUpQueue>(File.ReadAllText(path), JsonOptions);
    }

    public static string GetLatestReportPath(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "archive-followup.json");
    }

    private static OperationalHistoryArchiveFollowUpEntry BuildEntry(OperationalHistoryArchiveReadinessEntry entry)
    {
        var groupId = ResolveGroupId(entry);
        var taskId = Path.GetFileNameWithoutExtension(entry.OriginalPath.Replace('\\', '/'));
        var recommendedAction = groupId switch
        {
            "provider_evidence" => "Inspect candidate routing or promotion evidence before relying only on hot-window summaries.",
            "review_evidence" => "Inspect milestone review evidence before relying only on hot-window summaries.",
            _ => "Inspect archived evidence before discarding follow-up context.",
        };
        return new OperationalHistoryArchiveFollowUpEntry
        {
            FollowUpId = $"archive-followup-{groupId}-{taskId}".ToLowerInvariant(),
            GroupId = groupId,
            TaskId = taskId,
            OriginalPath = entry.OriginalPath,
            ArchivedAt = entry.ArchivedAt,
            WhyArchived = entry.WhyArchived,
            PromotionReason = entry.PromotionReason,
            RecommendedAction = recommendedAction,
            Summary = $"{taskId}: {entry.PromotionReason}",
        };
    }

    private static OperationalHistoryArchiveFollowUpGroup BuildGroup(string groupId, OperationalHistoryArchiveFollowUpEntry[] entries)
    {
        var title = groupId switch
        {
            "provider_evidence" => "Provider Evidence Follow-Up",
            "review_evidence" => "Review Evidence Follow-Up",
            _ => "Archived Evidence Follow-Up",
        };
        var recommendedAction = entries.FirstOrDefault()?.RecommendedAction ?? "Inspect archived evidence.";
        return new OperationalHistoryArchiveFollowUpGroup
        {
            GroupId = groupId,
            Title = title,
            ItemCount = entries.Length,
            RecommendedAction = recommendedAction,
            Summary = $"{title}: {entries.Length} queued item(s).",
        };
    }

    private static string ResolveGroupId(OperationalHistoryArchiveReadinessEntry entry)
    {
        var path = entry.OriginalPath.Replace('\\', '/');
        if (path.Contains("/provider/", StringComparison.OrdinalIgnoreCase))
        {
            return "provider_evidence";
        }

        if (path.Contains("/reviews/", StringComparison.OrdinalIgnoreCase))
        {
            return "review_evidence";
        }

        return "archived_evidence";
    }

    private void Persist(OperationalHistoryArchiveFollowUpQueue queue)
    {
        var path = GetLatestReportPath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(queue, JsonOptions));
    }
}
