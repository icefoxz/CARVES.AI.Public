using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineRecomputeServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void Persist_RecomputesEvidenceAndReadinessArtifactsFromFrozenCohort()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var cohort = CreateCohort("phase_0a_baseline");
        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-P0A-001", Title = "Phase 0A baseline recompute", Status = DomainTaskStatus.Completed },
        ]));

        WriteAttributionRecord(paths, CreateRecord(
            attributionId: "REQENV-001",
            requestId: "request-001",
            requestKind: "worker",
            taskId: "T-P0A-001",
            wholeRequestTokensEst: 70,
            contextWindowInputTokensTotal: 70,
            billableInputTokensUncached: 60,
            providerReportedInputTokens: 70,
            recordedAtUtc: new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero),
            segments:
            [
                Segment("seg-goal", "goal", "context-pack-parent", 20),
                Segment("seg-recall", "recall", "context-pack-parent", 25, trimmed: true, trimBefore: 35, trimAfter: 25),
                Segment("seg-tool", "tool_schema", null, 15),
                Segment("seg-system", "system", null, 10),
            ]));

        var formatterService = new RuntimeTokenBaselineEvidenceResultFormatterService(
            paths,
            new RuntimeTokenBaselineAggregatorService(paths),
            new RuntimeTokenOutcomeBinderService(
                new LlmRequestEnvelopeAttributionService(paths),
                new JsonTaskGraphRepository(paths),
                new ExecutionRunReportService(paths)));
        var service = new RuntimeTokenBaselineRecomputeService(
            paths,
            formatterService,
            new RuntimeTokenBaselineReadinessGateService(paths),
            new RuntimeTokenBaselineTrustLineService(paths));

        var result = service.Persist(cohort, new DateOnly(2026, 4, 21));

        Assert.Equal("ready_for_phase_1_target_work", result.ReadinessVerdict);
        Assert.True(result.Phase10TargetDecisionAllowed);
        Assert.Equal("recomputed_trusted_for_phase_1_target_decision", result.TrustLineClassification);
        Assert.True(result.SupersedesPreLedgerLine);
        Assert.False(result.CapBasedTargetDecisionAllowed);
        Assert.True(result.TotalCostClaimAllowed);
        Assert.Equal("proceed_renderer_shadow", result.RecommendationDecision);
        Assert.Equal("renderer_shadow_offline", result.RecommendationNextTrack);
        Assert.True(result.AdditionalCollectionRecommended);
        Assert.Contains("direct_cap_truth_missing", result.AdditionalCollectionReasons);

        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.CohortJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.EvidenceMarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.EvidenceJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.ReadinessMarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.ReadinessJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.TrustMarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.TrustJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));

        var recomputeMarkdownPath = Path.Combine(workspace.RootPath, "docs", "runtime", "runtime-token-optimization-phase-0a-ledger-recompute-result-2026-04-21.md");
        var recomputeJsonPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "token-optimization", "phase-0a", "ledger-recompute-result-2026-04-21.json");
        var trustMarkdownPath = Path.Combine(workspace.RootPath, "docs", "runtime", "runtime-token-optimization-phase-0a-trust-line-result-2026-04-21.md");
        var trustJsonPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "token-optimization", "phase-0a", "trust-line-result-2026-04-21.json");
        Assert.True(File.Exists(recomputeMarkdownPath));
        Assert.True(File.Exists(recomputeJsonPath));
        Assert.True(File.Exists(trustMarkdownPath));
        Assert.True(File.Exists(trustJsonPath));
        Assert.Contains("Phase 1.0 target decision allowed: `yes`", File.ReadAllText(recomputeMarkdownPath), StringComparison.Ordinal);
        Assert.Contains("Trust line classification: `recomputed_trusted_for_phase_1_target_decision`", File.ReadAllText(recomputeMarkdownPath), StringComparison.Ordinal);
        Assert.Contains("Phase 1.0 target decision may reference this line: `yes`", File.ReadAllText(trustMarkdownPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_CollectsDistinctAdditionalCollectionReasonsFromReadinessDimensions()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var cohort = CreateCohort("phase_0a_baseline");
        var evidenceResult = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            MarkdownArtifactPath = "docs/runtime/evidence.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/evidence.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = cohort,
                RequestCount = 1,
            },
            Recommendation = new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "insufficient_data",
                NextTrack = "insufficient_data",
            },
        };
        var readinessGateResult = new RuntimeTokenBaselineReadinessGateResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            Verdict = "insufficient_data",
            UnlocksPhase10TargetDecision = false,
            Readiness = new RuntimeTokenBaselineReadinessDimensions
            {
                AttributionShareReady = true,
                TaskCostReady = false,
                RouteReinjectionReady = false,
                CapTruthReady = false,
                Phase10TargetDecisionAllowed = true,
                TotalCostClaimAllowed = false,
                AttributionShareBlockingReasons = Array.Empty<string>(),
                TaskCostBlockingReasons = ["mandatory_included_requests_unbound"],
                RouteReinjectionBlockingReasons = ["operator_readback_reinjection_not_observed"],
                CapTruthBlockingReasons = ["direct_cap_truth_missing"],
                ActiveCanaryBlockingReasons = ["phase_0a_cannot_unlock_active_canary"],
            },
        };
        var draftRecomputeResult = RuntimeTokenBaselineRecomputeService.Persist(
            paths,
            cohort,
            evidenceResult,
            readinessGateResult,
            new RuntimeTokenBaselineTrustLineResult
            {
                ResultDate = new DateOnly(2026, 4, 21),
                CohortId = cohort.CohortId,
                TrustLineClassification = "placeholder",
                EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
                EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
                ReadinessMarkdownArtifactPath = "docs/runtime/readiness.md",
                ReadinessJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/readiness.json",
                RecomputeMarkdownArtifactPath = "docs/runtime/recompute.md",
                RecomputeJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/recompute.json",
            },
            new DateOnly(2026, 4, 21),
            recomputedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));
        var trustLineResult = RuntimeTokenBaselineTrustLineService.Persist(
            paths,
            evidenceResult,
            readinessGateResult,
            draftRecomputeResult,
            new DateOnly(2026, 4, 21),
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 5, 0, TimeSpan.Zero));

        var result = RuntimeTokenBaselineRecomputeService.Persist(
            paths,
            cohort,
            evidenceResult,
            readinessGateResult,
            trustLineResult,
            new DateOnly(2026, 4, 21),
            recomputedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        Assert.False(result.Phase10TargetDecisionAllowed);
        Assert.Equal("recomputed_but_insufficient_data_for_phase_1_target_decision", result.TrustLineClassification);
        Assert.True(result.AdditionalCollectionRecommended);
        Assert.Equal(
            ["direct_cap_truth_missing", "mandatory_included_requests_unbound", "operator_readback_reinjection_not_observed"],
            result.AdditionalCollectionReasons);
        Assert.Contains("Recommendation is still insufficient_data after recompute.", result.Notes);
    }

    private static RuntimeTokenBaselineCohortFreeze CreateCohort(string cohortId)
    {
        return new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = cohortId,
            WindowStartUtc = new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 21, 23, 59, 0, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "provider_actual_preferred_with_reconciliation",
            ContextWindowView = "context_window_input_tokens_total",
            BillableCostView = "billable_input_tokens_uncached",
        };
    }

    private static void WriteAttributionRecord(ControlPlanePaths paths, LlmRequestEnvelopeTelemetryRecord record)
    {
        Directory.CreateDirectory(paths.RuntimeRequestEnvelopeAttributionRoot);
        var path = Path.Combine(paths.RuntimeRequestEnvelopeAttributionRoot, $"{record.AttributionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
    }

    private static LlmRequestEnvelopeTelemetryRecord CreateRecord(
        string attributionId,
        string requestId,
        string requestKind,
        string? taskId,
        int wholeRequestTokensEst,
        int contextWindowInputTokensTotal,
        int billableInputTokensUncached,
        int? providerReportedInputTokens,
        DateTimeOffset recordedAtUtc,
        IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> segments)
    {
        var segmentTotal = segments.Sum(item => item.TokensEst);
        return new LlmRequestEnvelopeTelemetryRecord
        {
            AttributionId = attributionId,
            RequestId = requestId,
            RequestKind = requestKind,
            RequestKindEnumVersion = "runtime_request_kind.v1",
            Model = "gpt-5-mini",
            Provider = "openai",
            ProviderApiVersion = "responses_v1",
            Tokenizer = "local_estimator_v1",
            RequestSerializerVersion = "runtime_request_serializer.v1",
            TokenAccountingSource = "provider_actual",
            TaskId = taskId,
            WholeRequestTokensEst = wholeRequestTokensEst,
            SumSegmentTokensEst = segmentTotal,
            UnattributedTokensEst = Math.Max(0, wholeRequestTokensEst - segmentTotal),
            KnownProviderOverheadClass = "provider_serialization_delta",
            ContextWindowInputTokensTotal = contextWindowInputTokensTotal,
            BillableInputTokensUncached = billableInputTokensUncached,
            CachedInputTokens = providerReportedInputTokens.HasValue ? Math.Max(0, contextWindowInputTokensTotal - billableInputTokensUncached) : null,
            OutputTokens = 10,
            TotalContextTokensPerRequest = contextWindowInputTokensTotal + 10,
            TotalBillableTokensPerRequest = billableInputTokensUncached + 10,
            ProviderReportedInputTokens = providerReportedInputTokens,
            ProviderReportedUncachedInputTokens = providerReportedInputTokens.HasValue ? billableInputTokensUncached : null,
            ProviderReportedOutputTokens = 10,
            ProviderReportedTotalTokens = providerReportedInputTokens.HasValue ? contextWindowInputTokensTotal + 10 : null,
            EstimatedCostUsd = 0.01m,
            PricingVersion = "test",
            PricingSource = "test",
            CostEstimationVersion = "test",
            Segments = segments,
            RecordedAtUtc = recordedAtUtc,
        };
    }

    private static LlmRequestEnvelopeTelemetrySegment Segment(
        string segmentId,
        string segmentKind,
        string? parentId,
        int tokensEst,
        bool trimmed = false,
        int? trimBefore = null,
        int? trimAfter = null)
    {
        return new LlmRequestEnvelopeTelemetrySegment
        {
            SegmentId = segmentId,
            SegmentKind = segmentKind,
            SegmentParentId = parentId,
            SegmentOrder = 0,
            PayloadPath = $"$.segments.{segmentId}",
            SerializationKind = segmentKind == "tool_schema" ? "tool_schema_json" : "chat_message_text",
            Chars = tokensEst * 4,
            TokensEst = tokensEst,
            Included = true,
            Trimmed = trimmed,
            TrimBeforeTokensEst = trimBefore,
            TrimAfterTokensEst = trimAfter,
            ContentHash = $"hash-{segmentId}",
            HashMode = "hmac_sha256_env_scoped",
            HashSaltScope = "runtime_live_state",
            HmacKeyId = "token_telemetry_hmac.key",
            HashAlgorithm = "hmac_sha256",
            NormalizationVersion = "runtime_telemetry_norm.v1",
            RendererVersion = "prose_v1",
        };
    }
}
