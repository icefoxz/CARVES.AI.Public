using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryAdmissionGateServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void BlockWorkerDirectMemoryWrite_PersistsCertificateAndTelemetryBeforeWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryAdmissionGateService(workspace.Paths);

        var result = service.BlockWorkerDirectMemoryWrite(
            "T-MEM-ADMISSION-001",
            "RUN-T-MEM-ADMISSION-001-001",
            [".ai/memory/architecture/example.md", "src/Synthetic/Allowed.cs"]);

        Assert.Equal("block", result.Decision);
        Assert.Equal("protected_truth_root_write", result.ReasonCode);
        Assert.False(result.WritebackAllowed);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.CertificatePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.EventPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".ai", "memory", "architecture", "example.md")));

        var certificate = ReadJson<MemoryAdmissionCertificateRecord>(workspace.RootPath, result.CertificatePath);
        var telemetry = ReadJson<MemoryGateEventRecord>(workspace.RootPath, result.EventPath);
        Assert.Equal(MemoryAdmissionGateService.CertificateSchema, certificate.Schema);
        Assert.Equal(result.TelemetryEventId, certificate.TelemetryEventId);
        Assert.Equal("worker_patch", certificate.Entrypoint);
        Assert.Equal("path", certificate.Subject.Kind);
        Assert.Equal(MemoryAdmissionGateService.EventSchema, telemetry.Schema);
        Assert.Equal("memory.write.blocked", telemetry.EventType);
        Assert.Equal("effect_ledger_jsonl", telemetry.SinkFamily);
        Assert.Equal(result.TelemetryEventId, telemetry.EventId);

        var replay = new EffectLedgerService(workspace.Paths).ReplayOpen(
            Path.Combine(workspace.RootPath, result.LedgerPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(replay.CanWriteBack);
        Assert.Equal(1, replay.EventCount);
        Assert.Contains(result.TelemetryEventId, replay.EventIds);
        Assert.Contains("memory_write_blocked", replay.StepEvents);
    }

    [Fact]
    public void StageWorkerClaimCandidate_DowngradesToCandidateOnlyWithoutCanonicalFact()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryAdmissionGateService(workspace.Paths);

        var result = service.StageWorkerClaimCandidate(
            "T-MEM-ADMISSION-002",
            "RUN-T-MEM-ADMISSION-002-001",
            new MemoryAdmissionClaimRequest(
                "worker-claim-001",
                "task_lesson",
                "Worker noticed guarded memory boundary.",
                "Stage a worker-originated memory-like claim for review.",
                "Workers cannot directly write canonical memory truth.",
                "repo:CARVES.Runtime",
                ["evidence:worker-claim-001"])
            {
                TargetMemoryPath = ".ai/memory/architecture/runtime-memory-boundary.md",
                TaskScope = "T-MEM-ADMISSION-002",
                Confidence = 0.61,
            });

        Assert.Equal("candidate_only", result.Decision);
        Assert.Equal("worker_claim_low_trust", result.ReasonCode);
        Assert.False(result.WritebackAllowed);
        Assert.NotNull(result.Candidate);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.MemoryInboxRoot, $"{result.Candidate!.CandidateId}.json")));
        Assert.False(Directory.Exists(workspace.Paths.EvidenceFactsRoot));

        var certificate = ReadJson<MemoryAdmissionCertificateRecord>(workspace.RootPath, result.CertificatePath);
        var telemetry = ReadJson<MemoryGateEventRecord>(workspace.RootPath, result.EventPath);
        Assert.Equal("worker_claim", certificate.Entrypoint);
        Assert.Equal("stage_candidate", certificate.RequestedEffect);
        Assert.Equal("memory.claim.staged", telemetry.EventType);
        Assert.Equal("worker", telemetry.Actor);

        var replay = new EffectLedgerService(workspace.Paths).ReplayOpen(
            Path.Combine(workspace.RootPath, result.LedgerPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(1, replay.EventCount);
        Assert.Contains(result.TelemetryEventId, replay.EventIds);
    }

    [Fact]
    public void BlockReviewApprovalCanonicalization_EmitsTelemetryAndDoesNotPromoteFacts()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryAdmissionGateService(workspace.Paths);

        var result = service.BlockReviewApprovalCanonicalization(
            "T-MEM-ADMISSION-003",
            "RUN-T-MEM-ADMISSION-003-001",
            new MemoryAdmissionReviewRequest(
                "RTREV-T-MEM-ADMISSION-003",
                ["evidence:review-approval-001"])
            {
                TargetFactId = "MEMFACT-REVIEW-001",
                TargetMemoryPath = ".ai/memory/architecture/runtime-memory-boundary.md",
            });

        Assert.Equal("block", result.Decision);
        Assert.Equal("review_approval_not_promotion", result.ReasonCode);
        Assert.False(result.WritebackAllowed);
        Assert.False(Directory.Exists(workspace.Paths.EvidenceFactsRoot));
        Assert.False(Directory.Exists(workspace.Paths.MemoryPromotionsRoot));

        var certificate = ReadJson<MemoryAdmissionCertificateRecord>(workspace.RootPath, result.CertificatePath);
        var telemetry = ReadJson<MemoryGateEventRecord>(workspace.RootPath, result.EventPath);
        Assert.Equal("review_approval", certificate.Entrypoint);
        Assert.Equal("promote_fact", certificate.RequestedEffect);
        Assert.Equal("review.memory.no_canonical_write", telemetry.EventType);
        Assert.Equal("review", telemetry.Actor);
    }

    [Fact]
    public void StageHandoffClaimCandidate_RemainsLowTrustAndDoesNotCreateCanonicalFact()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryAdmissionGateService(workspace.Paths);

        var result = service.StageHandoffClaimCandidate(
            "T-MEM-ADMISSION-004",
            "RUN-T-MEM-ADMISSION-004-001",
            "HND-001",
            new MemoryAdmissionClaimRequest(
                "handoff-claim-001",
                "execution_observation",
                "Handoff carries a continuity-only memory clue.",
                "Stage a handoff completed fact as candidate-only.",
                "The current admission gate work should be continued before any canonical memory promotion.",
                "repo:CARVES.Runtime",
                ["evidence:handoff-claim-001"])
            {
                TargetMemoryPath = ".ai/memory/execution/runtime-memory-admission.md",
                TaskScope = "T-MEM-ADMISSION-004",
                Confidence = 0.57,
            });

        Assert.Equal("candidate_only", result.Decision);
        Assert.Equal("low_trust_continuity_packet", result.ReasonCode);
        Assert.False(result.WritebackAllowed);
        Assert.NotNull(result.Candidate);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.MemoryInboxRoot, $"{result.Candidate!.CandidateId}.json")));
        Assert.False(Directory.Exists(workspace.Paths.EvidenceFactsRoot));

        var certificate = ReadJson<MemoryAdmissionCertificateRecord>(workspace.RootPath, result.CertificatePath);
        var telemetry = ReadJson<MemoryGateEventRecord>(workspace.RootPath, result.EventPath);
        Assert.Equal("handoff_claim", certificate.Entrypoint);
        Assert.Equal("handoff_packet", certificate.Subject.Kind);
        Assert.Equal("memory.claim.staged", telemetry.EventType);
        Assert.Equal("handoff", telemetry.Actor);
    }

    private static T ReadJson<T>(string repoRoot, string relativePath)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize '{relativePath}' as {typeof(T).Name}.");
    }
}
