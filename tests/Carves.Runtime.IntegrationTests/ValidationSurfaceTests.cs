using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed class ValidationSurfaceTests
{
    [Fact]
    public void ColdValidationSurfaces_RenderCatalogTraceAndSummary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var repository = new JsonRoutingValidationRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        repository.SaveCatalog(new RoutingValidationCatalog
        {
            CatalogId = "catalog-surface",
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-SURFACE-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize the failure.",
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-surface",
            RunId = "val-run-surface",
            TaskId = "VAL-SURFACE-001",
            TaskType = "failure-summary",
            RoutingIntent = "failure_summary",
            ModuleId = "Execution/Failures",
            ExecutionMode = RoutingValidationMode.Routing,
            RoutingProfileId = "candidate-current-connected-lanes",
            RouteSource = "active_profile_preferred",
            SelectedProvider = "openai",
            SelectedLane = "groq-chat",
            SelectedBackend = "openai_api",
            SelectedModel = "llama-3.3-70b-versatile",
            SelectedRoutingProfileId = "worker-codegen-fast",
            AppliedRoutingRuleId = "rule-failure-summary",
            CodexThreadId = "codex-thread-surface",
            CodexThreadContinuity = WorkerThreadContinuity.ResumedThread,
            FallbackConfigured = true,
            FallbackTriggered = false,
            PreferredRouteEligibility = RouteEligibilityStatus.Eligible,
            SelectedBecause = ["preferred_route_eligible", "within_token_budget"],
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            EndedAt = DateTimeOffset.UtcNow,
            LatencyMs = 1200,
            RequestSucceeded = true,
            TaskSucceeded = true,
            SchemaValid = true,
            PromptTokens = 100,
            CompletionTokens = 30,
            EstimatedCostUsd = 0.000123m,
            ArtifactPaths = [".ai/validation/traces/trace-surface.json"],
            Candidates =
            [
                new RoutingValidationCandidateSnapshot
                {
                    BackendId = "openai_api",
                    ProviderId = "openai",
                    RoutingProfileId = "worker-codegen-fast",
                    RoutingRuleId = "rule-failure-summary",
                    RouteDisposition = "preferred",
                    Eligibility = RouteEligibilityStatus.Eligible,
                    Selected = true,
                    Signals = new RouteSelectionSignals
                    {
                        RouteHealth = "Healthy",
                        QuotaState = RouteQuotaState.Healthy,
                        TokenBudgetFit = true,
                        RecentLatencyMs = 1200,
                        RecentFailureCount = 0,
                    },
                    Reason = "matched preferred route via rule 'rule-failure-summary'",
                },
            ],
        });
        repository.SaveLatestSummary(new RoutingValidationSummary
        {
            RunId = "val-run-surface",
            ExecutionMode = RoutingValidationMode.Routing,
            RoutingProfileId = "candidate-current-connected-lanes",
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.0,
            AverageLatencyMs = 1200,
            TotalEstimatedCostUsd = 0.000123m,
            RouteBreakdown =
            [
                new RoutingValidationRouteBreakdown
                {
                    TaskFamily = "failure-summary",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    SelectedLane = "groq-chat",
                    SelectedModel = "llama-3.3-70b-versatile",
                    Samples = 1,
                    SuccessRate = 1.0,
                    PatchAcceptanceRate = 0.0,
                    AverageRetryCount = 0,
                    AverageLatencyMs = 1200,
                },
            ],
        });
        repository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-surface",
            ExecutionMode = RoutingValidationMode.Routing,
            RoutingProfileId = "candidate-current-connected-lanes",
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.0,
            AverageLatencyMs = 1200,
            TotalEstimatedCostUsd = 0.000123m,
            RouteBreakdown =
            [
                new RoutingValidationRouteBreakdown
                {
                    TaskFamily = "failure-summary",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    SelectedLane = "groq-chat",
                    SelectedModel = "llama-3.3-70b-versatile",
                    Samples = 1,
                    SuccessRate = 1.0,
                    PatchAcceptanceRate = 0.0,
                    AverageRetryCount = 0,
                    AverageLatencyMs = 1200,
                },
            ],
        });
        repository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-baseline",
            ExecutionMode = RoutingValidationMode.Baseline,
            RoutingProfileId = "candidate-current-connected-lanes",
            Tasks = 1,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.0,
            AverageLatencyMs = 900,
            TotalEstimatedCostUsd = 0.000111m,
        });

        var inspectSuite = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "validation-suite");
        var inspectTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "validation-trace", "trace-surface");
        var inspectSummary = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "validation-summary");
        var inspectHistory = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "validation-history");
        var apiSuite = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "validation-suite");
        var apiTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "validation-trace", "trace-surface");
        var apiSummary = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "validation-summary");
        var apiHistory = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "validation-history");

        Assert.Equal(0, inspectSuite.ExitCode);
        Assert.Contains("Validation catalog:", inspectSuite.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("VAL-SURFACE-001", inspectSuite.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Latest summary: val-run-surface", inspectSuite.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectTrace.ExitCode);
        Assert.Contains("Validation trace: trace-surface", inspectTrace.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Selected lane: groq-chat", inspectTrace.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Codex thread continuity: ResumedThread", inspectTrace.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Selected because:", inspectTrace.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectSummary.ExitCode);
        Assert.Contains("Validation summary:", inspectSummary.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Success rate: 100 %".Replace(" ", string.Empty), inspectSummary.StandardOutput.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains("Route breakdown:", inspectSummary.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("failure-summary -> openai/openai_api/groq-chat/llama-3.3-70b-versatile", inspectSummary.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, inspectHistory.ExitCode);
        Assert.Contains("Validation history:", inspectHistory.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("val-run-surface", inspectHistory.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("val-run-baseline", inspectHistory.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Latest route breakdown:", inspectHistory.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiSuite.ExitCode);
        Assert.Contains("\"task_id\": \"VAL-SURFACE-001\"", apiSuite.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiTrace.ExitCode);
        Assert.Contains("\"trace_id\": \"trace-surface\"", apiTrace.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"selected_lane\": \"groq-chat\"", apiTrace.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"codex_thread_continuity\": \"ResumedThread\"", apiTrace.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiSummary.ExitCode);
        Assert.Contains("\"run_id\": \"val-run-surface\"", apiSummary.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"route_breakdown\"", apiSummary.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiHistory.ExitCode);
        Assert.Contains("\"run_id\": \"val-run-surface\"", apiHistory.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"run_id\": \"val-run-baseline\"", apiHistory.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ColdCoverageAndReadinessSurfaces_ProjectMissingEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(paths);
        qualificationRepository.SaveCandidate(new ModelQualificationCandidateProfile
        {
            CandidateId = "candidate-surface-readiness",
            MatrixId = "matrix-surface-readiness",
            SourceRunId = "qual-run-surface-readiness",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-profile-surface-readiness",
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
        });

        var repository = new JsonRoutingValidationRepository(paths);
        repository.SaveCatalog(new RoutingValidationCatalog
        {
            CatalogId = "catalog-readiness",
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize the failure.",
                    BaselineLaneId = "n1n-responses",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return a tiny patch plan.",
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });
        repository.SaveTrace(new RoutingValidationTrace
        {
            TraceId = "trace-fs-baseline",
            RunId = "val-run-surface-a",
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
            RunId = "val-run-surface-b",
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
            RunId = "val-run-surface-b",
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
            RunId = "val-run-surface-a",
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
            RunId = "val-run-surface-b",
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
            RunId = "val-run-surface-a",
            ExecutionMode = RoutingValidationMode.Baseline,
            Tasks = 2,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
        });
        repository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "val-run-surface-b",
            ExecutionMode = RoutingValidationMode.Routing,
            Tasks = 3,
            SuccessRate = 1.0,
            SchemaValidityRate = 1.0,
            FallbackRate = 0.5,
        });

        var inspectCoverage = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "validation-coverage");
        var apiCoverage = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "validation-coverage");
        var inspectReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "promotion-readiness");
        var apiReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "promotion-readiness");

        Assert.Equal(0, inspectCoverage.ExitCode);
        Assert.Contains("Validation coverage matrix:", inspectCoverage.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("code.small.fix", inspectCoverage.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("missing_fallback_coverage", inspectCoverage.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiCoverage.ExitCode);
        Assert.Contains("\"task_family\": \"code.small.fix\"", apiCoverage.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"missing_fallback_coverage\"", apiCoverage.StandardOutput, StringComparison.Ordinal);

        Assert.True(inspectReadiness.ExitCode is 0 or 1);
        Assert.Contains("Routing candidate readiness:", inspectReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Status: partially_ready", inspectReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("missing_fallback_coverage", inspectReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("code.small.fix", inspectReadiness.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, apiReadiness.ExitCode);
        Assert.Contains("\"status\": \"partially_ready\"", apiReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"missing_fallback_coverage\"", apiReadiness.StandardOutput, StringComparison.Ordinal);
    }
}
