using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RoutingPromotionDecisionServiceTests
{
    [Fact]
    public void Evaluate_ReturnsEligibleDecisionWhenBaselineRoutingAndFallbackEvidenceExist()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveCandidate(new ModelQualificationCandidateProfile
        {
            CandidateId = "routing-candidate-eligible",
            MatrixId = "matrix-alpha",
            SourceRunId = "qual-run-001",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-matrix-alpha",
            },
            Intents =
            [
                new ModelQualificationIntentSummary
                {
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    PreferredLaneId = "groq-chat",
                    FallbackLaneIds = ["deepseek-chat"],
                },
            ],
        });

        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveTrace(CreateTrace("trace-baseline", "run-a", RoutingValidationMode.Baseline, "n1n-responses"));
        validationRepository.SaveTrace(CreateTrace("trace-routing", "run-b", RoutingValidationMode.Routing, "groq-chat"));
        validationRepository.SaveTrace(CreateTrace("trace-fallback", "run-b", RoutingValidationMode.ForcedFallback, "deepseek-chat", fallbackTriggered: true));
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "run-a",
            ExecutionMode = RoutingValidationMode.Baseline,
            Tasks = 1,
            SuccessRate = 1,
            SchemaValidityRate = 1,
            FallbackRate = 0,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "run-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 2,
            SuccessRate = 1,
            SchemaValidityRate = 1,
            FallbackRate = 0.5,
        });

        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubQualificationLaneExecutor());
        var validationService = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubQualificationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));
        var service = new RoutingPromotionDecisionService(qualificationService, validationService);

        var decision = service.Evaluate();

        Assert.True(decision.Eligible);
        Assert.True(decision.MultiBatchEvidence);
        Assert.Equal(2, decision.EvidenceBatchCount);
        Assert.Equal(1, decision.BaselineComparisonCount);
        Assert.Equal(1, decision.FallbackEvidenceCount);
        Assert.Empty(decision.ReasonCodes);
        Assert.Single(decision.Intents);
        Assert.Equal("trace-routing", decision.Intents[0].RoutingTraceId);
    }

    [Fact]
    public void Evaluate_BlocksPromotionWhenBaselineEvidenceIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveCandidate(new ModelQualificationCandidateProfile
        {
            CandidateId = "routing-candidate-missing-baseline",
            MatrixId = "matrix-alpha",
            SourceRunId = "qual-run-001",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-matrix-alpha",
            },
            Intents =
            [
                new ModelQualificationIntentSummary
                {
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    PreferredLaneId = "groq-chat",
                    FallbackLaneIds = ["deepseek-chat"],
                },
            ],
        });

        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveTrace(CreateTrace("trace-routing", "run-b", RoutingValidationMode.Routing, "groq-chat"));
        validationRepository.SaveTrace(CreateTrace("trace-fallback", "run-b", RoutingValidationMode.ForcedFallback, "deepseek-chat", fallbackTriggered: true));
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "run-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 2,
            SuccessRate = 1,
            SchemaValidityRate = 1,
            FallbackRate = 0.5,
        });

        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubQualificationLaneExecutor());
        var validationService = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubQualificationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));
        var service = new RoutingPromotionDecisionService(qualificationService, validationService);

        var decision = service.Evaluate();

        Assert.False(decision.Eligible);
        Assert.Contains("missing_baseline_comparison", decision.ReasonCodes);
    }

    private static RoutingValidationTrace CreateTrace(
        string traceId,
        string runId,
        RoutingValidationMode mode,
        string selectedLane,
        bool fallbackTriggered = false)
    {
        return new RoutingValidationTrace
        {
            TraceId = traceId,
            RunId = runId,
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "failure_summary",
            ModuleId = "Execution/Failures",
            ExecutionMode = mode,
            RoutingProfileId = "candidate-matrix-alpha",
            RouteSource = mode switch
            {
                RoutingValidationMode.Baseline => "validation_baseline",
                RoutingValidationMode.ForcedFallback => "active_profile_fallback",
                _ => "active_profile_preferred",
            },
            SelectedProvider = "openai",
            SelectedLane = selectedLane,
            SelectedBackend = "openai_api",
            SelectedModel = selectedLane,
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
            BuildOutcome = RoutingValidationExecutionOutcome.NotRun,
            TestOutcome = RoutingValidationExecutionOutcome.NotRun,
            SafetyOutcome = RoutingValidationExecutionOutcome.NotRun,
            FallbackTriggered = fallbackTriggered,
        };
    }

    private sealed class StubQualificationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            return WorkerExecutionResult.Skipped(
                qualificationCase.CaseId,
                lane.BackendId,
                lane.ProviderId,
                "stub",
                WorkerExecutionProfile.UntrustedDefault,
                "not used",
                string.Empty,
                string.Empty);
        }
    }

    private static WorkerSelectionPolicyService CreateSelectionService(string rootPath)
    {
        var paths = ControlPlanePaths.FromRepoRoot(rootPath);
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(paths));
        var boundary = new WorkerExecutionBoundaryService(rootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(rootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var health = new ProviderHealthMonitorService(new JsonProviderHealthRepository(paths), providers, adapters);
        return new WorkerSelectionPolicyService(rootPath, repoRegistry, providers, routing, governance, adapters, boundary, health, operationalPolicyService);
    }
}
