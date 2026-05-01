using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenWrapperPolicyInventoryServiceTests
{
    [Fact]
    public void Persist_BuildsWrapperInventoryFromTrustedPhase10Line()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_trusted",
            WindowStartUtc = new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 21, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker", "planner"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };
        var evidence = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/evidence.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/evidence.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = cohort,
            },
            Recommendation = new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "reprioritize_to_wrapper",
                NextTrack = "wrapper_policy_shadow_offline",
            },
        };
        var trust = new RuntimeTokenBaselineTrustLineResult
        {
            ResultDate = resultDate,
            CohortId = cohort.CohortId,
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10TargetDecisionMayReferenceThisLine = true,
        };
        var phase10 = new RuntimeTokenPhase10TargetDecisionResult
        {
            ResultDate = resultDate,
            CohortId = cohort.CohortId,
            MarkdownArtifactPath = "docs/runtime/phase10.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/phase10.json",
            TrustLineClassification = trust.TrustLineClassification,
            Phase10TargetDecisionMayReferenceThisLine = true,
            Decision = "reprioritize_to_wrapper",
            NextTrack = "wrapper_policy_shadow_offline",
        };
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-001",
                requestId: "worker-request-001",
                requestKind: "worker",
                wholeRequestTokensEst: 400,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("system_instructions", "system", "$.instructions", "system", "developer_policy_text", "worker_request_serializer.v1", 100, "hash-shared-system"),
                    Segment("context_pack", "context_pack", "$.input.context_pack", "user", "context_pack_text", "prose_v1", 180, "hash-context-a"),
                ]),
            CreateRecord(
                attributionId: "REQENV-002",
                requestId: "worker-request-002",
                requestKind: "worker",
                wholeRequestTokensEst: 420,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 2, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("system_instructions", "system", "$.instructions", "system", "developer_policy_text", "worker_request_serializer.v1", 100, "hash-shared-system"),
                    Segment("context_pack", "context_pack", "$.input.context_pack", "user", "context_pack_text", "prose_v1", 200, "hash-context-b"),
                ]),
            CreateRecord(
                attributionId: "REQENV-003",
                requestId: "planner-request-001",
                requestKind: "planner",
                wholeRequestTokensEst: 500,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 3, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("planner_system", "system", "$.messages[0].content.system", "system", "developer_policy_text", "planner_request_serializer.v1", 100, "hash-shared-system"),
                    Segment("planner_output_contract", "planner_output_contract", "$.messages[0].content.output_contract", "user", "chat_message_text", "planner_request_serializer.v1", 60, "hash-output-contract"),
                    Segment("context_pack_json", "context_pack", "$.messages[0].content.context_pack_json", "user", "context_pack_text", "prose_v1", 220, "hash-context-c"),
                ]),
        };

        var result = RuntimeTokenWrapperPolicyInventoryService.Persist(
            paths,
            evidence,
            trust,
            phase10,
            records,
            resultDate);

        Assert.True(result.Phase11WrapperInventoryMayReferenceThisLine);
        Assert.Equal("reprioritize_to_wrapper", result.Phase10Decision);
        Assert.Equal("wrapper_policy_shadow_offline", result.Phase10NextTrack);
        Assert.Equal(["planner", "worker"], result.RequestKindsCovered);
        Assert.Contains("request_kind_not_covered:reviewer", result.CoverageLimitations);
        Assert.Equal(3, result.CohortRequestCount);
        Assert.NotEmpty(result.TopWrapperSurfaces);

        var workerSystem = Assert.Single(result.WrapperSurfaces, item =>
            string.Equals(item.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(item.SegmentKind, "system", StringComparison.Ordinal));
        Assert.Equal("structural_only", workerSystem.CompressionAllowed);
        Assert.True(workerSystem.PolicyCritical);
        Assert.True(workerSystem.RepeatedAcrossRequests);
        Assert.True(workerSystem.RepeatedAcrossRequestKinds);
        Assert.Equal("dedupe_and_request_kind_slice_review", workerSystem.RecommendedInventoryAction);

        var plannerOutput = Assert.Single(result.WrapperSurfaces, item =>
            string.Equals(item.RequestKind, "planner", StringComparison.Ordinal)
            && string.Equals(item.SegmentKind, "planner_output_contract", StringComparison.Ordinal));
        Assert.Equal("invariant_first", plannerOutput.RecommendedInventoryAction);
        Assert.Equal("structural_only", plannerOutput.CompressionAllowed);

        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static LlmRequestEnvelopeTelemetryRecord CreateRecord(
        string attributionId,
        string requestId,
        string requestKind,
        int wholeRequestTokensEst,
        DateTimeOffset recordedAtUtc,
        IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> segments)
    {
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
            WholeRequestTokensEst = wholeRequestTokensEst,
            SumSegmentTokensEst = segments.Sum(item => item.TokensEst),
            UnattributedTokensEst = Math.Max(0, wholeRequestTokensEst - segments.Sum(item => item.TokensEst)),
            ContextWindowInputTokensTotal = wholeRequestTokensEst,
            BillableInputTokensUncached = wholeRequestTokensEst,
            TotalContextTokensPerRequest = wholeRequestTokensEst,
            TotalBillableTokensPerRequest = wholeRequestTokensEst,
            Segments = segments,
            RecordedAtUtc = recordedAtUtc,
        };
    }

    private static LlmRequestEnvelopeTelemetrySegment Segment(
        string segmentId,
        string segmentKind,
        string payloadPath,
        string role,
        string serializationKind,
        string rendererVersion,
        int tokensEst,
        string contentHash)
    {
        return new LlmRequestEnvelopeTelemetrySegment
        {
            SegmentId = segmentId,
            SegmentKind = segmentKind,
            SegmentOrder = 0,
            Role = role,
            PayloadPath = payloadPath,
            SerializationKind = serializationKind,
            Chars = tokensEst * 4,
            TokensEst = tokensEst,
            Included = true,
            ContentHash = contentHash,
            HashMode = "hmac_sha256_env_scoped",
            HashSaltScope = "runtime_live_state",
            HmacKeyId = "token_telemetry_hmac.key",
            HashAlgorithm = "hmac_sha256",
            NormalizationVersion = "runtime_telemetry_norm.v1",
            RendererVersion = rendererVersion,
        };
    }
}
