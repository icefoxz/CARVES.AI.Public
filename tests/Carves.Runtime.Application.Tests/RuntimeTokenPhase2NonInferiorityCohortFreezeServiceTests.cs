using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2NonInferiorityCohortFreezeServiceTests
{
    [Fact]
    public void Persist_FreezesWorkerOnlyNonInferiorityCohort()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
        };
        var proof = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CrossKindProofVerdict = "proof_available_for_worker_only_canary_scope",
            CrossKindProofAvailable = true,
            CanaryRequestKindAllowlist = ["worker"],
            PolicyCriticalFragmentCount = 7,
            PolicyCriticalFragmentRemovedCount = 0,
        };
        var rollback = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-canary-rollback-plan-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-canary-rollback-plan-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CandidateVersion = "wrapper_candidate_20260421_worker_system___instructions",
            FallbackVersion = "original_worker_system_instructions",
            RollbackPlanReviewed = true,
            RollbackTestPlanDefined = true,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = ["worker"],
        };
        var workerRecollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            CohortJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-cohort-2026-04-21.json",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = candidate.CohortId,
                WindowStartUtc = new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
                WindowEndUtc = new DateTimeOffset(2026, 4, 21, 23, 59, 0, TimeSpan.Zero),
                RequestKinds = ["worker"],
                TokenAccountingSourcePolicy = "local_estimate_only",
                ContextWindowView = "context_window_input_tokens_total",
                BillableCostView = "billable_input_tokens_uncached",
            },
            RequestedTaskCount = 1,
            RecollectedTaskCount = 1,
            AttributionRecordCount = 1,
            DirectToLlmRouteEdgeCount = 1,
            TaskIds = ["T-WORKER-001"],
            AttributionIds = ["REQENV-001"],
            Tasks =
            [
                new RuntimeTokenBaselineWorkerRecollectTaskRecord
                {
                    TaskId = "T-WORKER-001",
                    RunId = "RUN-T-WORKER-001-001",
                    RequestId = "worker-request-001",
                    AttributionId = "REQENV-001",
                    PacketArtifactPath = ".ai/runtime/execution-packets/T-WORKER-001.json",
                    ContextPackArtifactPath = ".ai/runtime/context-packs/tasks/T-WORKER-001.json",
                    Consumer = "TestRepo",
                    TokenAccountingSource = "local_estimate",
                },
            ],
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 1,
                SuccessfulAttemptedTaskCount = 1,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
                AttemptedTaskIds = ["T-WORKER-001"],
                Tasks =
                [
                    new RuntimeTokenBaselineAttemptedTaskRecord
                    {
                        TaskId = "T-WORKER-001",
                        RunId = "RUN-T-WORKER-001-001",
                        WorkerBackend = "null_worker",
                        TaskStatus = "Completed",
                        LatestRunStatus = "Completed",
                        Attempted = true,
                        SuccessfulAttempted = true,
                        ReviewAdmissionAccepted = true,
                        ConstraintViolationObserved = false,
                    },
                ],
            },
        };
        var trustLine = new RuntimeTokenBaselineTrustLineResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            SupersedesPreLedgerLine = true,
            EvidenceMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-2026-04-21.md",
            EvidenceJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/attribution-baseline-evidence-result-2026-04-21.json",
            ReadinessMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-readiness-gate-result-2026-04-21.md",
            ReadinessJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/readiness-gate-result-2026-04-21.json",
            RecomputeMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-ledger-recompute-result-2026-04-21.md",
            RecomputeJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/ledger-recompute-result-2026-04-21.json",
            Phase10TargetDecisionMayReferenceThisLine = true,
        };
        var attributionRecord = new LlmRequestEnvelopeTelemetryRecord
        {
            AttributionId = "REQENV-001",
            RequestId = "worker-request-001",
            RequestKind = "worker",
            Provider = "openai",
            ProviderApiVersion = "responses_v1",
            Model = "gpt-5-mini",
            Tokenizer = "local_estimator_v1",
            TokenAccountingSource = "local_estimate",
            RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero),
        };

        var result = RuntimeTokenPhase2NonInferiorityCohortFreezeService.Persist(
            workspace.Paths,
            candidate,
            proof,
            rollback,
            workerRecollect,
            trustLine,
            [attributionRecord],
            resultDate);

        Assert.True(result.NonInferiorityCohortFrozen);
        Assert.Equal("openai", result.Provider);
        Assert.Equal("responses_v1", result.ProviderApiVersion);
        Assert.Equal("gpt-5-mini", result.Model);
        Assert.Equal("local_estimator_v1", result.Tokenizer);
        var requestKindMix = Assert.Single(result.RequestKindMix);
        Assert.Equal("worker", requestKindMix.RequestKind);
        Assert.Equal(1, requestKindMix.RequestCount);
        Assert.Equal(1.0, requestKindMix.RequestRatio, 6);
        Assert.Contains(result.MetricThresholds, threshold => string.Equals(threshold.MetricId, "task_success_rate", StringComparison.Ordinal));
        Assert.Empty(result.BlockingReasons);
        Assert.True(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet);
        Assert.Equal(1, result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount);
        Assert.Contains("TestRepo", result.ToolAvailability);
        Assert.Contains("windows_powershell", result.ToolAvailability);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
