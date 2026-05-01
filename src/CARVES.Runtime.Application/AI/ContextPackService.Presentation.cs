using System.Text;
using System.Text.Json;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private static string BuildGoal(TaskNode task)
    {
        return string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
    }

    private string BuildTaskSummary(TaskNode task)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(task.CardId))
        {
            var cardSummary = TryLoadCardSummary(task.CardId);
            if (!string.IsNullOrWhiteSpace(cardSummary))
            {
                builder.AppendLine($"Card: {cardSummary}");
            }
        }

        builder.AppendLine($"Task ID: {task.TaskId}");
        builder.AppendLine($"Title: {task.Title}");
        builder.AppendLine($"Description: {task.Description}");
        builder.AppendLine("Acceptance:");
        foreach (var item in task.Acceptance)
        {
            builder.AppendLine($"- {item}");
        }

        return builder.ToString().Trim();
    }

    private static string BuildPlannerTaskSummary(
        string wakeDetail,
        OpportunitySnapshot snapshot,
        IReadOnlyList<Opportunity> selectedOpportunities,
        IReadOnlyList<TaskNode> previewTasks)
    {
        var builder = new StringBuilder()
            .AppendLine($"Wake detail: {wakeDetail}")
            .AppendLine($"Open opportunities: {snapshot.Items.Count(item => item.Status == OpportunityStatus.Open)}")
            .AppendLine($"Selected opportunities: {selectedOpportunities.Count}");

        foreach (var opportunity in selectedOpportunities.Take(5))
        {
            builder.AppendLine($"- {opportunity.OpportunityId}: {opportunity.Title} [{opportunity.Source}]");
        }

        if (previewTasks.Count > 0)
        {
            builder.AppendLine("Preview tasks:");
            foreach (var task in previewTasks.Take(5))
            {
                builder.AppendLine($"- {task.TaskId}: {task.Title}");
            }
        }

        return builder.ToString().Trim();
    }

    private string? TryLoadCardSummary(string cardId)
    {
        var path = Path.Combine(paths.CardsRoot, $"{cardId}.md");
        if (!File.Exists(path))
        {
            return null;
        }

        var lines = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            return null;
        }

        var titleIndex = Array.FindIndex(lines, line => string.Equals(line, "## Title", StringComparison.OrdinalIgnoreCase));
        var goalIndex = Array.FindIndex(lines, line => string.Equals(line, "## Goal", StringComparison.OrdinalIgnoreCase));
        var title = titleIndex >= 0 && titleIndex + 1 < lines.Length ? lines[titleIndex + 1] : lines.FirstOrDefault();
        var goal = goalIndex >= 0 && goalIndex + 1 < lines.Length ? lines[goalIndex + 1] : null;
        return string.IsNullOrWhiteSpace(goal) ? title : $"{title} - {goal}";
    }

    private void Persist(ContextPack pack, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(pack, JsonOptions));
    }

    private static ContextPackScopeItem ToScopeItem(TaskNode task)
    {
        return new ContextPackScopeItem
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Status = task.Status.ToString(),
            Summary = ResolveTaskScopeSummary(task),
        };
    }

    private static string? ResolveTaskScopeSummary(TaskNode task)
    {
        var summary = !string.IsNullOrWhiteSpace(task.LastWorkerSummary)
            ? task.LastWorkerSummary
            : task.PlannerReview is { AcceptanceMet: true } && !string.IsNullOrWhiteSpace(task.PlannerReview.Reason)
                ? task.PlannerReview.Reason
                : null;

        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        summary = summary.ReplaceLineEndings(" ").Trim();
        return summary.Length <= 1200 ? summary : Truncate(summary, 1200);
    }

    private string GetPlannerPackPath(string packId)
    {
        return Path.Combine(paths.AiRoot, "runtime", "context-packs", "planner", $"{packId}.json");
    }

    private static IReadOnlyList<ContextPackWindowedRead> ToWindowedReads(BoundedReadProjection projection)
    {
        return projection.WindowedReads
            .Select(item => new ContextPackWindowedRead
            {
                Path = item.Path,
                TotalLines = item.TotalLines,
                StartLine = item.StartLine,
                EndLine = item.EndLine,
                Reason = item.Reason,
                Truncated = item.Truncated,
                Snippet = item.Snippet,
            })
            .ToArray();
    }

    private static ContextPackCompaction NewCompaction(BoundedReadProjection projection)
    {
        return new ContextPackCompaction
        {
            Strategy = projection.Strategy,
            CandidateFileCount = projection.CandidateFileCount,
            RelevantFileCount = projection.RelevantFiles.Count,
            WindowedReadCount = projection.WindowedReads.Count,
            FullReadCount = projection.FullReadCount,
            OmittedFileCount = projection.OmittedFileCount,
        };
    }

    private static ContextPackCompaction BuildCompaction(ContextPackCompaction baseline, int windowedReadCount, int trimmedItemCount)
    {
        return baseline with
        {
            WindowedReadCount = windowedReadCount,
            TrimmedItemCount = trimmedItemCount,
        };
    }

    private string ToRuntimeRelativePath(string path)
    {
        return Path.GetRelativePath(paths.RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }
}
