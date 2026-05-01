using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed class QualificationSurfaceTests
{
    [Fact]
    public void ColdQualificationSurfaces_InspectMaterializePromoteCandidateTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var repository = new JsonCurrentModelQualificationRepository(paths);
        var validationRepository = new JsonRoutingValidationRepository(paths);
        repository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-surface",
            Summary = "Surface qualification matrix",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "gemini-native-balanced",
                    ProviderId = "gemini",
                    BackendId = "gemini_api",
                    RequestFamily = "generate_content",
                    Model = "gemini-2.5-pro",
                    BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                    ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
                    RoutingProfileId = "gemini-worker-balanced",
                    RouteGroup = "gemini_native",
                    ObservedVariance = "low",
                },
                new ModelQualificationLane
                {
                    LaneId = "n1n-responses",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-4.1",
                    BaseUrl = "https://hk.n1n.ai/v1",
                    ApiKeyEnvironmentVariable = "N1N_API_KEY",
                    RoutingProfileId = "worker-codegen-fast",
                    RouteGroup = "n1n",
                    ObservedVariance = "high",
                },
            ],
            Cases =
            [
                new ModelQualificationCase
                {
                    CaseId = "patch-draft",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return a patch plan.",
                },
            ],
        });
        repository.SaveLatestRun(new ModelQualificationRunLedger
        {
            RunId = "qual-run-surface",
            MatrixId = "matrix-surface",
            Results =
            [
                new ModelQualificationResult
                {
                    RunId = "qual-run-surface",
                    LaneId = "gemini-native-balanced",
                    CaseId = "patch-draft",
                    Attempt = 1,
                    ProviderId = "gemini",
                    BackendId = "gemini_api",
                    RequestFamily = "generate_content",
                    Model = "gemini-2.5-pro",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Success = true,
                    FormatValid = true,
                    QualityScore = 5,
                    LatencyMs = 420,
                },
                new ModelQualificationResult
                {
                    RunId = "qual-run-surface",
                    LaneId = "n1n-responses",
                    CaseId = "patch-draft",
                    Attempt = 1,
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-4.1",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Success = true,
                    FormatValid = true,
                    QualityScore = 4,
                    LatencyMs = 580,
                },
            ],
        });
        validationRepository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-baseline-surface",
            RunId = "val-run-surface-a",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
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
            TraceId = "trace-routing-surface",
            RunId = "val-run-surface-b",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "patch_draft",
            ModuleId = "Execution/ResultEnvelope",
            ExecutionMode = RoutingValidationMode.Routing,
            SelectedLane = "gemini-native-balanced",
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        validationRepository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-fallback-surface",
            RunId = "val-run-surface-b",
            TaskId = "VAL-FS-001",
            TaskType = "failure-summary",
            RoutingIntent = "patch_draft",
            ModuleId = "Execution/ResultEnvelope",
            ExecutionMode = RoutingValidationMode.ForcedFallback,
            SelectedLane = "n1n-responses",
            FallbackTriggered = true,
            TaskSucceeded = true,
            RequestSucceeded = true,
            SchemaValid = true,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-surface-a",
            ExecutionMode = RoutingValidationMode.Baseline,
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.0,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-surface-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 2,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.5,
        });

        var inspectQualification = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "qualification");
        var apiQualification = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "qualification");
        var materialize = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "qualification", "materialize-candidate");
        var inspectCandidate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "qualification-candidate");
        var inspectPromotion = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "qualification-promotion");
        var promote = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "qualification", "promote-candidate");
        var inspectRouting = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "routing-profile");

        Assert.Equal(0, inspectQualification.ExitCode);
        Assert.Contains("Qualification matrix: matrix-surface", inspectQualification.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Latest run: qual-run-surface", inspectQualification.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiQualification.ExitCode);
        Assert.Contains("\"matrix_id\": \"matrix-surface\"", apiQualification.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"run_id\": \"qual-run-surface\"", apiQualification.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, materialize.ExitCode);
        Assert.Contains("Candidate routing profile:", materialize.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectCandidate.ExitCode);
        Assert.Contains("Candidate routing profile:", inspectCandidate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("preferred=gemini-native-balanced", inspectCandidate.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectPromotion.ExitCode);
        Assert.Contains("Eligible: True", inspectPromotion.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, promote.ExitCode);
        Assert.Contains("Promoted candidate routing profile to active profile.", promote.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectRouting.ExitCode);
        Assert.Contains("Active routing profile: candidate-matrix-surface", inspectRouting.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Source qualification: routing-candidate-", inspectRouting.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("generate_content", inspectRouting.StandardOutput, StringComparison.Ordinal);
    }
}
