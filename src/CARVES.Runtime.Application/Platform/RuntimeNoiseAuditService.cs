using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeNoiseAuditService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan LegacyWindow = TimeSpan.FromDays(7);

    private readonly TaskGraphService taskGraphService;
    private readonly Func<IReadOnlyList<RuntimeIncidentRecord>> incidentAccessor;

    public RuntimeNoiseAuditService(TaskGraphService taskGraphService, OperatorApiService operatorApiService)
        : this(taskGraphService, () => operatorApiService.GetRuntimeIncidents())
    {
    }

    public RuntimeNoiseAuditService(TaskGraphService taskGraphService, Func<IReadOnlyList<RuntimeIncidentRecord>> incidentAccessor)
    {
        this.taskGraphService = taskGraphService;
        this.incidentAccessor = incidentAccessor;
    }

    public RuntimeNoiseAuditReport Build()
    {
        var now = DateTimeOffset.UtcNow;
        var graph = taskGraphService.Load();
        var tasks = graph.ListTasks()
            .Where(task => task.Status is DomainTaskStatus.Blocked or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait)
            .Select(task => new RuntimeNoiseAuditTaskEntry
            {
                TaskId = task.TaskId,
                Status = task.Status.ToString(),
                Classification = Classify(task, now),
                Reason = Describe(task),
            })
            .OrderByDescending(item => item.Classification)
            .ThenBy(item => item.TaskId, StringComparer.Ordinal)
            .ToArray();
        var taskLookup = graph.Tasks.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var incidents = incidentAccessor()
            .Select(incident => new RuntimeNoiseAuditIncidentEntry
            {
                IncidentId = incident.IncidentId,
                IncidentType = incident.IncidentType.ToString(),
                TaskId = incident.TaskId,
                Classification = ClassifyReference(incident.TaskId, incident.OccurredAt, taskLookup, now),
                Reason = Describe(incident, taskLookup),
            })
            .OrderByDescending(item => item.Classification)
            .ThenByDescending(item => item.IncidentId, StringComparer.Ordinal)
            .ToArray();

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["active_blocker"] = tasks.Count(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
                + incidents.Count(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker),
            ["projection_noise"] = tasks.Count(item => item.Classification == RuntimeNoiseAuditClassification.ProjectionNoise)
                + incidents.Count(item => item.Classification == RuntimeNoiseAuditClassification.ProjectionNoise),
            ["legacy_debt"] = tasks.Count(item => item.Classification == RuntimeNoiseAuditClassification.LegacyDebt)
                + incidents.Count(item => item.Classification == RuntimeNoiseAuditClassification.LegacyDebt),
        };

        return new RuntimeNoiseAuditReport
        {
            StartGate = counts["active_blocker"] > 0
                ? RuntimeNoiseStartGateVerdict.Blocked
                : counts["projection_noise"] + counts["legacy_debt"] > 0
                    ? RuntimeNoiseStartGateVerdict.ClearWithNoise
                    : RuntimeNoiseStartGateVerdict.Clear,
            BlockedTasks = tasks,
            Incidents = incidents,
            ClassificationCounts = counts,
        };
    }

    public RuntimeNoiseAuditClassification ClassifyTaskReference(string? taskId, DateTimeOffset occurredAt)
    {
        var graph = taskGraphService.Load();
        var taskLookup = graph.Tasks.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return ClassifyReference(taskId, occurredAt, taskLookup, DateTimeOffset.UtcNow);
    }

    private static RuntimeNoiseAuditClassification Classify(TaskNode task, DateTimeOffset now)
    {
        if (task.Status is DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait)
        {
            return RuntimeNoiseAuditClassification.ActiveBlocker;
        }

        var age = now - task.UpdatedAt;
        if (age <= ActiveWindow)
        {
            return RuntimeNoiseAuditClassification.ActiveBlocker;
        }

        return age >= LegacyWindow
            ? RuntimeNoiseAuditClassification.LegacyDebt
            : RuntimeNoiseAuditClassification.ProjectionNoise;
    }

    private static RuntimeNoiseAuditClassification ClassifyReference(
        string? taskId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, TaskNode> taskLookup,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(taskId)
            && taskLookup.TryGetValue(taskId, out var task)
            && task.Status is DomainTaskStatus.Blocked or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait or DomainTaskStatus.Running)
        {
            return RuntimeNoiseAuditClassification.ActiveBlocker;
        }

        var age = now - occurredAt;
        if (age >= LegacyWindow)
        {
            return RuntimeNoiseAuditClassification.LegacyDebt;
        }

        return RuntimeNoiseAuditClassification.ProjectionNoise;
    }

    private static string Describe(TaskNode task)
    {
        if (!string.IsNullOrWhiteSpace(task.LastRecoveryReason))
        {
            return task.LastRecoveryReason!;
        }

        return task.Status switch
        {
            DomainTaskStatus.Review => task.PlannerReview.Reason ?? "Waiting for review resolution.",
            DomainTaskStatus.ApprovalWait => "Waiting for permission approval.",
            _ => task.PlannerReview.Reason ?? task.LastWorkerSummary ?? "Blocked task requires reconciliation.",
        };
    }

    private static string Describe(RuntimeIncidentRecord incident, IReadOnlyDictionary<string, TaskNode> taskLookup)
    {
        if (!string.IsNullOrWhiteSpace(incident.TaskId)
            && taskLookup.TryGetValue(incident.TaskId, out var task))
        {
            return $"{incident.Summary} (task status: {task.Status})";
        }

        return incident.Summary;
    }
}
