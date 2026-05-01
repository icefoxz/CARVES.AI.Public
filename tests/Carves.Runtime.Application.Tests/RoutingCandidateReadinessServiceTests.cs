using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RoutingCandidateReadinessServiceTests
{
    [Fact]
    public void Build_ProjectsPartialReadinessAndNextActions()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveCandidate(new ModelQualificationCandidateProfile
        {
            CandidateId = "candidate-readiness",
            MatrixId = "matrix-readiness",
            SourceRunId = "qual-run-readiness",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-readiness-profile",
            },
            Intents =
            [
                new ModelQualificationIntentSummary
                {
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    PreferredLaneId = "groq-chat",
                    FallbackLaneIds = ["n1n-responses"],
                },
            ],
        });
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "patch",
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });
        validationRepository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-baseline",
            RunId = "val-run-a",
            TaskId = "VAL-CODE-001",
            TaskType = "code.small.fix",
            RoutingIntent = "patch_draft",
            ModuleId = "Execution/ResultEnvelope",
            ExecutionMode = RoutingValidationMode.Baseline,
            SelectedLane = "n1n-responses",
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        validationRepository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-routing",
            RunId = "val-run-b",
            TaskId = "VAL-CODE-001",
            TaskType = "code.small.fix",
            RoutingIntent = "patch_draft",
            ModuleId = "Execution/ResultEnvelope",
            ExecutionMode = RoutingValidationMode.Routing,
            SelectedLane = "groq-chat",
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-a",
            ExecutionMode = RoutingValidationMode.Baseline,
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
        });

        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            new JsonRuntimeRoutingProfileRepository(workspace.Paths),
            new NoOpQualificationLaneExecutor());
        var validationService = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new NoOpQualificationLaneExecutor(),
            ValidationCoverageMatrixServiceTests_RoutingValidationServiceTests_CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(new JsonRuntimeRoutingProfileRepository(workspace.Paths)));
        var promotionDecisionService = new RoutingPromotionDecisionService(qualificationService, validationService);
        var coverageMatrixService = new ValidationCoverageMatrixService(qualificationService, validationService);
        var readinessService = new RoutingCandidateReadinessService(promotionDecisionService, coverageMatrixService);

        var readiness = readinessService.Build();

        Assert.Equal("partially_ready", readiness.Status);
        Assert.False(readiness.PromotionEligible);
        Assert.Contains(readiness.BlockingReasons, reason => reason == "missing_fallback_coverage");
        Assert.Contains(readiness.RecommendedNextActions, action => action == "run forced-fallback validation for code.small.fix");
        var family = Assert.Single(readiness.Families.Where(item => item.TaskFamily == "code.small.fix"));
        Assert.Equal("code.small.fix", family.TaskFamily);
        Assert.Equal("partially_ready", family.Status);
        Assert.Contains(family.BlockingReasons, reason => reason == "missing_fallback_coverage");
    }

    private sealed class NoOpQualificationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            throw new InvalidOperationException("This test should not execute qualification lanes.");
        }
    }

    private static WorkerSelectionPolicyService ValidationCoverageMatrixServiceTests_RoutingValidationServiceTests_CreateSelectionService(string rootPath)
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
