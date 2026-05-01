using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Failures;

public sealed class FailureSummaryService
{
    private readonly FailureContextService contextService;

    public FailureSummaryService(FailureContextService contextService)
    {
        this.contextService = contextService;
    }

    public FailureSummarySnapshot Build(string? taskId = null)
    {
        var failures = string.IsNullOrWhiteSpace(taskId)
            ? contextService.LoadAll()
            : contextService.GetTaskFailures(taskId, int.MaxValue);

        var topTypes = failures
            .GroupBy(item => item.Failure.Type.ToString(), StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new FailureCountSummary(group.Key, group.Count()))
            .ToArray();
        var topTasks = failures
            .Where(item => !string.IsNullOrWhiteSpace(item.TaskId))
            .GroupBy(item => item.TaskId!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new FailureCountSummary(group.Key, group.Count()))
            .ToArray();
        var topFiles = failures
            .SelectMany(item => item.InputSummary.FilesInvolved)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .GroupBy(path => path, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new FailureCountSummary(group.Key, group.Count()))
            .ToArray();
        var providerRates = failures
            .Where(item => !string.IsNullOrWhiteSpace(item.Provider))
            .GroupBy(item => item.Provider!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FailureProviderRateSummary(
                group.Key,
                group.Count(),
                failures.Count == 0 ? 0 : group.Count() / (double)failures.Count))
            .ToArray();

        return new FailureSummarySnapshot
        {
            TaskId = taskId,
            TotalFailures = failures.Count,
            TopTypes = topTypes,
            TopTasks = topTasks,
            TopFiles = topFiles,
            ProviderFailureRates = providerRates,
            RecentFailures = failures
                .OrderByDescending(item => item.Timestamp)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Take(10)
                .ToArray(),
        };
    }

    public IReadOnlyList<string> RenderLines(FailureSummarySnapshot summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.TaskId))
        {
            var lines = new List<string>
            {
                $"Failures for {summary.TaskId}:",
                $"Total failures: {summary.TotalFailures}",
            };

            foreach (var failure in summary.RecentFailures)
            {
                lines.Add($"- {failure.Id} [{failure.Failure.Type}] {failure.Failure.Message}");
            }

            if (summary.RecentFailures.Count == 0)
            {
                lines.Add("- (none)");
            }

            return lines;
        }

        return
        [
            "Failure Summary:",
            $"Total failures: {summary.TotalFailures}",
            string.Empty,
            "Top types:",
            .. RenderCountBlock(summary.TopTypes),
            string.Empty,
            "Top tasks:",
            .. RenderCountBlock(summary.TopTasks),
            string.Empty,
            "Top files:",
            .. RenderCountBlock(summary.TopFiles),
            string.Empty,
            "Provider failure rate:",
            .. RenderProviderRates(summary.ProviderFailureRates),
        ];
    }

    private static IReadOnlyList<string> RenderCountBlock(IReadOnlyList<FailureCountSummary> counts)
    {
        if (counts.Count == 0)
        {
            return ["- (none)"];
        }

        return counts.Take(5)
            .Select(item => $"- {item.Key} ({item.Count})")
            .ToArray();
    }

    private static IReadOnlyList<string> RenderProviderRates(IReadOnlyList<FailureProviderRateSummary> providers)
    {
        if (providers.Count == 0)
        {
            return ["- (none)"];
        }

        return providers.Take(5)
            .Select(item => $"- {item.ProviderId} = {item.Rate:0.00}")
            .ToArray();
    }
}

public sealed class FailureSummarySnapshot
{
    public string? TaskId { get; init; }

    public int TotalFailures { get; init; }

    public IReadOnlyList<FailureCountSummary> TopTypes { get; init; } = Array.Empty<FailureCountSummary>();

    public IReadOnlyList<FailureCountSummary> TopTasks { get; init; } = Array.Empty<FailureCountSummary>();

    public IReadOnlyList<FailureCountSummary> TopFiles { get; init; } = Array.Empty<FailureCountSummary>();

    public IReadOnlyList<FailureProviderRateSummary> ProviderFailureRates { get; init; } = Array.Empty<FailureProviderRateSummary>();

    public IReadOnlyList<FailureReport> RecentFailures { get; init; } = Array.Empty<FailureReport>();
}

public sealed record FailureCountSummary(string Key, int Count);

public sealed record FailureProviderRateSummary(string ProviderId, int FailureCount, double Rate);
