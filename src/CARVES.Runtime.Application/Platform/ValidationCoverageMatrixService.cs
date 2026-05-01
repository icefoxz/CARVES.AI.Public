using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ValidationCoverageMatrixService
{
    private readonly CurrentModelQualificationService qualificationService;
    private readonly RoutingValidationService routingValidationService;

    public ValidationCoverageMatrixService(
        CurrentModelQualificationService qualificationService,
        RoutingValidationService routingValidationService)
    {
        this.qualificationService = qualificationService;
        this.routingValidationService = routingValidationService;
    }

    public ValidationCoverageMatrix Build(string? candidateId = null, int historyLimit = 10)
    {
        var candidate = qualificationService.LoadCandidate() ?? throw new InvalidOperationException("No candidate routing profile exists.");
        if (!string.IsNullOrWhiteSpace(candidateId) && !string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Candidate '{candidateId}' does not match the current candidate '{candidate.CandidateId}'.");
        }

        var catalog = routingValidationService.LoadOrCreateCatalog();
        var traces = routingValidationService.LoadTraces();
        var history = routingValidationService.LoadHistory(historyLimit);
        var families = catalog.Tasks
            .GroupBy(
                task => $"{task.TaskType}|{task.RoutingIntent}|{task.ModuleId ?? string.Empty}",
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => BuildFamily(group.ToArray(), candidate, traces))
            .ToArray();
        var missingEvidence = families
            .SelectMany(family => family.MissingEvidence)
            .ToArray();

        return new ValidationCoverageMatrix
        {
            CandidateId = candidate.CandidateId,
            ProfileId = candidate.Profile.ProfileId,
            ValidationBatchCount = history.BatchCount,
            Families = families,
            MissingEvidence = missingEvidence,
        };
    }

    private static ValidationCoverageFamily BuildFamily(
        RoutingValidationTaskDefinition[] tasks,
        ModelQualificationCandidateProfile candidate,
        IReadOnlyList<RoutingValidationTrace> traces)
    {
        var representative = tasks[0];
        var taskIds = tasks
            .Select(task => task.TaskId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(taskId => taskId, StringComparer.Ordinal)
            .ToArray();
        var familyTraces = traces
            .Where(trace => taskIds.Contains(trace.TaskId, StringComparer.Ordinal))
            .OrderByDescending(trace => trace.EndedAt)
            .ToArray();
        var intent = candidate.Intents.FirstOrDefault(item =>
            string.Equals(item.RoutingIntent, representative.RoutingIntent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.ModuleId ?? string.Empty, representative.ModuleId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        var preferredLaneId = intent?.PreferredLaneId ?? string.Empty;
        var fallbackLaneIds = intent?.FallbackLaneIds ?? [];
        var baselineTraceCount = familyTraces.Count(trace => trace.ExecutionMode == RoutingValidationMode.Baseline);
        var routingTraceCount = string.IsNullOrWhiteSpace(preferredLaneId)
            ? familyTraces.Count(trace => trace.ExecutionMode == RoutingValidationMode.Routing)
            : familyTraces.Count(trace =>
                trace.ExecutionMode == RoutingValidationMode.Routing
                && string.Equals(trace.SelectedLane, preferredLaneId, StringComparison.Ordinal));
        var fallbackRequired = fallbackLaneIds.Length > 0;
        var fallbackTraceCount = fallbackRequired
            ? familyTraces.Count(trace =>
                trace.ExecutionMode == RoutingValidationMode.ForcedFallback
                && fallbackLaneIds.Contains(trace.SelectedLane ?? string.Empty, StringComparer.Ordinal))
            : 0;
        var missingEvidence = BuildMissingEvidence(
            representative.TaskType,
            representative.RoutingIntent,
            representative.ModuleId,
            taskIds,
            intent,
            baselineTraceCount,
            routingTraceCount,
            fallbackRequired,
            fallbackTraceCount);

        return new ValidationCoverageFamily
        {
            TaskFamily = representative.TaskType,
            RoutingIntent = representative.RoutingIntent,
            ModuleId = representative.ModuleId,
            TaskIds = taskIds,
            PreferredLaneId = preferredLaneId,
            FallbackLaneIds = fallbackLaneIds,
            BaselineTraceCount = baselineTraceCount,
            RoutingTraceCount = routingTraceCount,
            FallbackTraceCount = fallbackTraceCount,
            BaselineCovered = baselineTraceCount > 0,
            RoutingCovered = routingTraceCount > 0,
            FallbackRequired = fallbackRequired,
            FallbackCovered = !fallbackRequired || fallbackTraceCount > 0,
            MissingEvidence = missingEvidence,
        };
    }

    private static ValidationCoverageGap[] BuildMissingEvidence(
        string taskFamily,
        string routingIntent,
        string? moduleId,
        string[] taskIds,
        ModelQualificationIntentSummary? intent,
        int baselineTraceCount,
        int routingTraceCount,
        bool fallbackRequired,
        int fallbackTraceCount)
    {
        var gaps = new List<ValidationCoverageGap>();
        if (intent is null)
        {
            gaps.Add(new ValidationCoverageGap
            {
                TaskFamily = taskFamily,
                RoutingIntent = routingIntent,
                ModuleId = moduleId,
                RequiredMode = RoutingValidationMode.Routing,
                ReasonCode = "missing_candidate_intent",
                TaskIds = taskIds,
                Summary = $"Candidate routing truth has no intent binding for {routingIntent} / {moduleId ?? "(none)"}.",
            });
            return gaps.ToArray();
        }

        if (baselineTraceCount == 0)
        {
            gaps.Add(new ValidationCoverageGap
            {
                TaskFamily = taskFamily,
                RoutingIntent = routingIntent,
                ModuleId = moduleId,
                RequiredMode = RoutingValidationMode.Baseline,
                ReasonCode = "missing_baseline_coverage",
                TaskIds = taskIds,
                Summary = $"Run baseline validation for {taskFamily} before promotion can compare against {intent.PreferredLaneId}.",
            });
        }

        if (routingTraceCount == 0)
        {
            gaps.Add(new ValidationCoverageGap
            {
                TaskFamily = taskFamily,
                RoutingIntent = routingIntent,
                ModuleId = moduleId,
                RequiredMode = RoutingValidationMode.Routing,
                ReasonCode = "missing_routing_coverage",
                TaskIds = taskIds,
                Summary = $"Run routing validation for {taskFamily} so preferred lane {intent.PreferredLaneId} has trace evidence.",
            });
        }

        if (fallbackRequired && fallbackTraceCount == 0)
        {
            gaps.Add(new ValidationCoverageGap
            {
                TaskFamily = taskFamily,
                RoutingIntent = routingIntent,
                ModuleId = moduleId,
                RequiredMode = RoutingValidationMode.ForcedFallback,
                ReasonCode = "missing_fallback_coverage",
                TaskIds = taskIds,
                Summary = $"Run forced-fallback validation for {taskFamily} so {string.Join(", ", intent.FallbackLaneIds)} has trace evidence.",
            });
        }

        return gaps.ToArray();
    }
}
