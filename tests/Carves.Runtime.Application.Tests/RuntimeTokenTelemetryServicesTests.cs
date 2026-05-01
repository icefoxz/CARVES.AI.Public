using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenTelemetryServicesTests
{
    [Fact]
    public void LlmRequestEnvelopeAttributionService_WritesCanonicalTelemetryRecord()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new LlmRequestEnvelopeAttributionService(workspace.Paths);
        var draft = new LlmRequestEnvelopeDraft
        {
            RequestId = "worker-request-001",
            RequestKind = "worker",
            Model = "gpt-5-mini",
            Provider = "openai",
            ProviderApiVersion = "responses_v1",
            Tokenizer = "local_estimator_v1",
            RequestSerializerVersion = "worker_request_serializer.v1",
            RunId = "RUN-001",
            TaskId = "T-001",
            PackId = "CP-001",
            WholeRequestText = "system\n\nuser",
            Segments =
            [
                new LlmRequestEnvelopeSegmentDraft
                {
                    SegmentId = "system",
                    SegmentKind = "system",
                    MessageIndex = 0,
                    Role = "system",
                    PayloadPath = "$.instructions",
                    SerializationKind = "developer_policy_text",
                    Content = "system",
                    SourceItemId = "T-001",
                    RendererVersion = "worker_request_serializer.v1",
                },
                new LlmRequestEnvelopeSegmentDraft
                {
                    SegmentId = "context_pack",
                    SegmentKind = "context_pack",
                    MessageIndex = 1,
                    Role = "user",
                    PayloadPath = "$.input.context_pack",
                    SerializationKind = "context_pack_text",
                    Content = "user",
                    SourceItemId = "CP-001",
                    RendererVersion = "prose_v1",
                },
            ],
        };

        var record = service.Record(
            draft,
            new LlmRequestEnvelopeUsage
            {
                TokenAccountingSource = "provider_actual",
                ProviderReportedInputTokens = 42,
                ProviderReportedCachedInputTokens = 12,
                ProviderReportedUncachedInputTokens = 30,
                ProviderReportedOutputTokens = 9,
                ProviderReportedTotalTokens = 51,
                KnownProviderOverheadClass = "provider_serialization_delta",
                InternalPromptBudgetCapHit = true,
                SectionBudgetCapHit = true,
                TrimLoopCapHit = false,
                CapTriggerSegmentKind = "recall",
                CapTriggerSource = "context_pack_budget_contributors",
            });

        Assert.Equal("provider_actual", record.TokenAccountingSource);
        Assert.Equal(42, record.ContextWindowInputTokensTotal);
        Assert.Equal(30, record.BillableInputTokensUncached);
        Assert.Equal(12, record.CachedInputTokens);
        Assert.Equal(51, record.TotalContextTokensPerRequest);
        Assert.Equal(51, record.TotalBillableTokensPerRequest);
        Assert.Null(record.ProviderContextCapHit);
        Assert.True(record.InternalPromptBudgetCapHit);
        Assert.True(record.SectionBudgetCapHit);
        Assert.False(record.TrimLoopCapHit);
        Assert.Equal("recall", record.CapTriggerSegmentKind);
        Assert.Equal("context_pack_budget_contributors", record.CapTriggerSource);
        var systemSegment = Assert.Single(record.Segments, segment => segment.SegmentId == "system");
        Assert.Equal("hmac_sha256_env_scoped", systemSegment.HashMode);
        Assert.Equal("runtime_live_state", systemSegment.HashSaltScope);
        Assert.Equal("hmac_sha256", systemSegment.HashAlgorithm);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.RuntimeRequestEnvelopeAttributionRoot, $"{record.AttributionId}.json")));
        Assert.Equal(record.AttributionId, Assert.Single(service.ListRecent()).AttributionId);
    }

    [Fact]
    public void RuntimeSurfaceRouteGraphService_MergesRepeatedEdgesAndPersistsHashMetadata()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeSurfaceRouteGraphService(workspace.Paths);

        var surface = service.RecordSurface(
            surfaceId: ".ai/runtime/context-packs/tasks/T-001.json",
            producer: "context_pack_service",
            surfaceKind: "candidate_context_surface",
            content: "Context Pack\nGoal:\nAlpha");

        service.RecordRouteEdge(new RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = surface.SurfaceId,
            Consumer = "worker:openai:responses_v1",
            DeclaredRouteKind = "direct_to_llm",
            ObservedRouteKind = "direct_to_llm",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            RetrievalHitCount = 0,
            LlmReinjectionCount = 1,
            AverageFanout = 1,
            EvidenceSource = "worker-request-001",
            LastSeen = DateTimeOffset.UtcNow,
        });
        service.RecordRouteEdge(new RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = surface.SurfaceId,
            Consumer = "worker:openai:responses_v1",
            DeclaredRouteKind = "direct_to_llm",
            ObservedRouteKind = "direct_to_llm",
            ObservedCount = 2,
            SampleCount = 2,
            FrequencyWindow = "7d",
            RetrievalHitCount = 3,
            LlmReinjectionCount = 2,
            AverageFanout = 2,
            EvidenceSource = "worker-request-002",
            LastSeen = DateTimeOffset.UtcNow,
        });

        var persistedSurface = Assert.Single(service.ListSurfaces());
        Assert.Equal("hmac_sha256_env_scoped", persistedSurface.HashMode);
        Assert.Equal("runtime_live_state", persistedSurface.HashSaltScope);
        Assert.Equal("token_telemetry_hmac.key", persistedSurface.HmacKeyId);
        var mergedEdge = Assert.Single(service.ListRouteEdges());
        Assert.Equal(3, mergedEdge.ObservedCount);
        Assert.Equal(3, mergedEdge.SampleCount);
        Assert.Equal(3, mergedEdge.RetrievalHitCount);
        Assert.Equal(3, mergedEdge.LlmReinjectionCount);
        Assert.Equal(5d / 3d, mergedEdge.AverageFanout, 6);
        Assert.True(File.Exists(workspace.Paths.RuntimeConsumerRouteGraphSurfacesFile));
        Assert.True(File.Exists(workspace.Paths.RuntimeConsumerRouteGraphEdgesFile));
    }
}
