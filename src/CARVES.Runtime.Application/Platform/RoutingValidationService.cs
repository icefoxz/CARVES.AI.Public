using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RoutingValidationService
{
    private readonly IRoutingValidationRepository repository;
    private readonly CurrentModelQualificationService currentModelQualificationService;
    private readonly IQualificationLaneExecutor laneExecutor;
    private readonly WorkerSelectionPolicyService workerSelectionPolicyService;
    private readonly RuntimeRoutingProfileService runtimeRoutingProfileService;

    public RoutingValidationService(
        IRoutingValidationRepository repository,
        CurrentModelQualificationService currentModelQualificationService,
        IQualificationLaneExecutor laneExecutor,
        WorkerSelectionPolicyService workerSelectionPolicyService,
        RuntimeRoutingProfileService runtimeRoutingProfileService)
    {
        this.repository = repository;
        this.currentModelQualificationService = currentModelQualificationService;
        this.laneExecutor = laneExecutor;
        this.workerSelectionPolicyService = workerSelectionPolicyService;
        this.runtimeRoutingProfileService = runtimeRoutingProfileService;
    }

    public RoutingValidationCatalog LoadOrCreateCatalog()
    {
        var catalog = repository.LoadCatalog();
        var desired = CreateDefaultCatalog();
        if (catalog is null)
        {
            repository.SaveCatalog(desired);
            return desired;
        }

        var merged = MergeCatalog(catalog, desired);
        if (!string.Equals(
                JsonSerializer.Serialize(catalog),
                JsonSerializer.Serialize(merged),
                StringComparison.Ordinal))
        {
            repository.SaveCatalog(merged);
            return merged;
        }

        return catalog;
    }

    public IReadOnlyList<RoutingValidationTrace> LoadTraces(string? runId = null)
    {
        return repository.LoadTraces(runId);
    }

    public RoutingValidationTrace? LoadTrace(string traceId)
    {
        return repository.LoadTrace(traceId);
    }

    public RoutingValidationSummary? LoadLatestSummary()
    {
        return repository.LoadLatestSummary();
    }

    public RoutingValidationSummary? LoadSummary(string runId)
    {
        return repository.LoadSummary(runId);
    }

    private sealed record ValidationRouteResolution(
        ModelQualificationLane? Lane,
        string RouteSource,
        bool FallbackConfigured,
        bool FallbackTriggered,
        RouteEligibilityStatus? PreferredRouteEligibility,
        string? PreferredIneligibilityReason,
        string[] SelectedBecause,
        string? AppliedRoutingRuleId,
        string? SelectedRoutingProfileId,
        RoutingValidationCandidateSnapshot[] Candidates)
    {
        public static ValidationRouteResolution None { get; } = new(
            null,
            "selection_failed",
            false,
            false,
            null,
            null,
            [],
            null,
            null,
            []);
    }

    private sealed record ValidationExecutionOutcomes(
        RoutingValidationExecutionOutcome BuildOutcome,
        RoutingValidationExecutionOutcome TestOutcome,
        RoutingValidationExecutionOutcome SafetyOutcome)
    {
        public static ValidationExecutionOutcomes NotRun { get; } = new(
            RoutingValidationExecutionOutcome.NotRun,
            RoutingValidationExecutionOutcome.NotRun,
            RoutingValidationExecutionOutcome.NotRun);
    }
}
