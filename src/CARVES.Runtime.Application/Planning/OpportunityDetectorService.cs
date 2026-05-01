using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Planning;

public sealed class OpportunityDetectorService
{
    private readonly IOpportunityRepository repository;
    private readonly IReadOnlyList<IOpportunityDetector> detectors;
    private readonly ICodeGraphBuilder? codeGraphBuilder;
    private readonly TaskGraphService? taskGraphService;

    public OpportunityDetectorService(
        IOpportunityRepository repository,
        IEnumerable<IOpportunityDetector> detectors,
        ICodeGraphBuilder? codeGraphBuilder = null,
        TaskGraphService? taskGraphService = null)
    {
        this.repository = repository;
        this.detectors = detectors.ToArray();
        this.codeGraphBuilder = codeGraphBuilder;
        this.taskGraphService = taskGraphService;
    }

    public OpportunitySnapshot LoadSnapshot()
    {
        return repository.Load();
    }

    public OpportunityDetectionResult DetectAndStore()
    {
        codeGraphBuilder?.Build();

        var now = DateTimeOffset.UtcNow;
        var existing = repository.Load();
        var opportunities = existing.Items.ToDictionary(
            item => BuildKey(item.Source, item.Fingerprint),
            item => item,
            StringComparer.Ordinal);
        var observedKeys = new HashSet<string>(StringComparer.Ordinal);
        var contributions = new List<OpportunityDetectorContribution>();

        foreach (var detector in detectors)
        {
            var ids = new List<string>();
            foreach (var observation in detector.Detect())
            {
                var key = BuildKey(observation.Source, observation.Fingerprint);
                observedKeys.Add(key);

                if (!opportunities.TryGetValue(key, out var opportunity))
                {
                    opportunity = new Opportunity
                    {
                        OpportunityId = BuildStableId(observation.Source, observation.Fingerprint, observation.Title),
                        Source = observation.Source,
                        Fingerprint = observation.Fingerprint,
                        FirstDetectedAt = now,
                    };
                    opportunities[key] = opportunity;
                }

                opportunity.MarkObserved(observation, now);
                ids.Add(opportunity.OpportunityId);
            }

            contributions.Add(new OpportunityDetectorContribution(detector.Name, ids));
        }

        foreach (var opportunity in opportunities.Values.Where(item => !observedKeys.Contains(BuildKey(item.Source, item.Fingerprint)) && item.Status != OpportunityStatus.Dismissed))
        {
            opportunity.MarkResolved("detector no longer observes this opportunity", now);
        }

        ReconcileMaterializedTaskLineage(opportunities.Values, now);

        var snapshot = new OpportunitySnapshot
        {
            Version = 1,
            GeneratedAt = now,
            Items = opportunities.Values
                .OrderBy(item => StatusWeight(item.Status))
                .ThenBy(item => SeverityWeight(item.Severity))
                .ThenBy(item => item.OpportunityId, StringComparer.Ordinal)
                .ToArray(),
        };
        repository.Save(snapshot);
        return new OpportunityDetectionResult(snapshot, contributions);
    }

    private void ReconcileMaterializedTaskLineage(IEnumerable<Opportunity> opportunities, DateTimeOffset observedAt)
    {
        if (taskGraphService is null)
        {
            return;
        }

        var graph = taskGraphService.Load();
        foreach (var opportunity in opportunities)
        {
            if (opportunity.Status is OpportunityStatus.Resolved or OpportunityStatus.Dismissed)
            {
                continue;
            }

            var materializedTaskIds = ResolveMaterializedTaskIds(opportunity, graph);
            if (materializedTaskIds.Count == 0)
            {
                continue;
            }

            if (opportunity.Status == OpportunityStatus.Materialized
                && opportunity.MaterializedTaskIds.SequenceEqual(materializedTaskIds, StringComparer.Ordinal))
            {
                continue;
            }

            opportunity.MarkMaterialized(materializedTaskIds, "materialized task lineage detected from current task graph", observedAt);
        }
    }

    private static IReadOnlyList<string> ResolveMaterializedTaskIds(Opportunity opportunity, DomainTaskGraph graph)
    {
        var fromMetadata = graph.Tasks.Values
            .Where(task => task.Metadata.TryGetValue("origin_opportunity_id", out var opportunityId)
                           && string.Equals(opportunityId, opportunity.OpportunityId, StringComparison.Ordinal))
            .Select(task => task.TaskId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (fromMetadata.Length > 0)
        {
            return OrderTaskIds(fromMetadata);
        }

        if (opportunity.Source != OpportunitySource.MemoryDrift
            || !opportunity.Metadata.TryGetValue("module", out var module)
            || string.IsNullOrWhiteSpace(module))
        {
            return Array.Empty<string>();
        }

        var expectedScope = $".ai/memory/modules/{module}.md";
        var moduleStem = module.Trim().Replace('_', '-');
        var expectedTaskPrefix = $"T-PLAN-audit-runtime-memory-for-{moduleStem}-";

        var fromLegacyTaskGraphDraft = graph.Tasks.Values
            .Where(task =>
                task.TaskId.StartsWith(expectedTaskPrefix, StringComparison.OrdinalIgnoreCase)
                && task.Scope.Any(path => string.Equals(NormalizePath(path), expectedScope, StringComparison.OrdinalIgnoreCase)))
            .Select(task => task.TaskId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return fromLegacyTaskGraphDraft.Length == 0 ? Array.Empty<string>() : OrderTaskIds(fromLegacyTaskGraphDraft);
    }

    private static IReadOnlyList<string> OrderTaskIds(IEnumerable<string> taskIds)
    {
        return taskIds
            .Select(taskId => new
            {
                TaskId = taskId,
                SortKey = BuildTaskSortKey(taskId),
            })
            .OrderBy(item => item.SortKey.BaseId, StringComparer.Ordinal)
            .ThenBy(item => item.SortKey.SuffixOrder)
            .ThenBy(item => item.TaskId, StringComparer.Ordinal)
            .Select(item => item.TaskId)
            .ToArray();
    }

    private static (string BaseId, int SuffixOrder) BuildTaskSortKey(string taskId)
    {
        var lastHyphen = taskId.LastIndexOf('-', taskId.Length - 1);
        if (lastHyphen <= 0 || taskId.Length - lastHyphen - 1 != 3)
        {
            return (taskId, 0);
        }

        var suffix = taskId[(lastHyphen + 1)..];
        return int.TryParse(suffix, out var parsed)
            ? (taskId[..lastHyphen], parsed)
            : (taskId, 0);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string BuildKey(OpportunitySource source, string fingerprint)
    {
        return $"{source}:{fingerprint}";
    }

    private static string BuildStableId(OpportunitySource source, string fingerprint, string title)
    {
        var fileStem = title
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('.', '-')
            .Replace('_', '-');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}|{fingerprint}"))).ToLowerInvariant()[..8];
        return $"OPP-{fileStem}-{hash}";
    }

    private static int StatusWeight(OpportunityStatus status)
    {
        return status switch
        {
            OpportunityStatus.Open => 0,
            OpportunityStatus.Materialized => 1,
            OpportunityStatus.Resolved => 2,
            OpportunityStatus.Dismissed => 3,
            _ => 99,
        };
    }

    private static int SeverityWeight(OpportunitySeverity severity)
    {
        return severity switch
        {
            OpportunitySeverity.Critical => 0,
            OpportunitySeverity.High => 1,
            OpportunitySeverity.Medium => 2,
            OpportunitySeverity.Low => 3,
            _ => 99,
        };
    }
}
