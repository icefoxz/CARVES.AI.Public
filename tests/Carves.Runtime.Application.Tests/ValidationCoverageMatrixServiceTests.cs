using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class ValidationCoverageMatrixServiceTests
{
    [Fact]
    public void Build_ProjectsMissingEvidencePerFamily()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveCandidate(CreateCandidate());
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "summarize",
                    BaselineLaneId = "n1n-responses",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "patch",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary"],
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });
        SeedTraceData(validationRepository);

        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            new JsonRuntimeRoutingProfileRepository(workspace.Paths),
            new NoOpQualificationLaneExecutor());
        var validationService = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new NoOpQualificationLaneExecutor(),
            RoutingValidationServiceTests_CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(new JsonRuntimeRoutingProfileRepository(workspace.Paths)));
        var service = new ValidationCoverageMatrixService(qualificationService, validationService);

        var matrix = service.Build();

        Assert.Equal("candidate-coverage", matrix.CandidateId);
        Assert.Equal(2, matrix.ValidationBatchCount);
        var failureSummary = Assert.Single(matrix.Families.Where(family => family.TaskFamily == "failure-summary"));
        Assert.True(failureSummary.BaselineCovered);
        Assert.True(failureSummary.RoutingCovered);
        Assert.True(failureSummary.FallbackCovered);
        Assert.Empty(failureSummary.MissingEvidence);

        var codeFix = Assert.Single(matrix.Families.Where(family => family.TaskFamily == "code.small.fix"));
        Assert.True(codeFix.BaselineCovered);
        Assert.True(codeFix.RoutingCovered);
        Assert.True(codeFix.FallbackRequired);
        Assert.False(codeFix.FallbackCovered);
        Assert.Contains(codeFix.MissingEvidence, gap => gap.ReasonCode == "missing_fallback_coverage");
        Assert.Contains(matrix.MissingEvidence, gap => gap.TaskFamily == "code.small.fix" && gap.RequiredMode == RoutingValidationMode.ForcedFallback);
    }

    private static ModelQualificationCandidateProfile CreateCandidate()
    {
        return new ModelQualificationCandidateProfile
        {
            CandidateId = "candidate-coverage",
            MatrixId = "matrix-coverage",
            SourceRunId = "qual-run-coverage",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-profile",
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
                new ModelQualificationIntentSummary
                {
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    PreferredLaneId = "groq-chat",
                    FallbackLaneIds = ["n1n-responses"],
                },
            ],
        };
    }

    private static void SeedTraceData(JsonRoutingValidationRepository repository)
    {
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-fs-baseline",
            RunId = "val-run-a",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "failure_summary",
            ModuleId = "Execution/Failures",
            ExecutionMode = RoutingValidationMode.Baseline,
            SelectedLane = "n1n-responses",
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-fs-routing",
            RunId = "val-run-b",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "failure_summary",
            ModuleId = "Execution/Failures",
            ExecutionMode = RoutingValidationMode.Routing,
            SelectedLane = "groq-chat",
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-fs-fallback",
            RunId = "val-run-b",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "failure_summary",
            ModuleId = "Execution/Failures",
            ExecutionMode = RoutingValidationMode.ForcedFallback,
            SelectedLane = "deepseek-chat",
            FallbackTriggered = true,
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-code-baseline",
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
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-code-routing",
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
        repository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-a",
            ExecutionMode = RoutingValidationMode.Baseline,
            Tasks = 2,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
        });
        repository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 3,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.5,
        });
    }

    private sealed class NoOpQualificationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            throw new InvalidOperationException("This test should not execute qualification lanes.");
        }
    }

    private static WorkerSelectionPolicyService RoutingValidationServiceTests_CreateSelectionService(string rootPath)
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
