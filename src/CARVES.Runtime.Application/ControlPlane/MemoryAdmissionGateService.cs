using System.Text.Json;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class MemoryAdmissionGateService
{
    public const string CertificateSchema = "memory_admission_certificate.v1";
    public const string EventSchema = "memory_gate_event.v1";
    public const string EffectLedgerSinkFamily = "effect_ledger_jsonl";
    public const string DefaultPolicyVersion = "memory-gate.v1.shadow";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly EffectLedgerService effectLedgerService;
    private readonly RuntimeMemoryPromotionService memoryPromotionService;

    public MemoryAdmissionGateService(
        ControlPlanePaths paths,
        EffectLedgerService? effectLedgerService = null,
        RuntimeMemoryPromotionService? memoryPromotionService = null)
    {
        this.paths = paths;
        this.effectLedgerService = effectLedgerService ?? new EffectLedgerService(paths);
        this.memoryPromotionService = memoryPromotionService ?? new RuntimeMemoryPromotionService(paths);
    }

    public string GetRunCertificateDirectory(string runId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId, "memory-admission-certificates");
    }

    public string GetRunEventDirectory(string runId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId, "memory-gate-events");
    }

    public MemoryAdmissionDecisionResult BlockWorkerDirectMemoryWrite(
        string taskId,
        string runId,
        IReadOnlyList<string> attemptedPaths)
    {
        var protectedPath = attemptedPaths
            .Select(NormalizeRepoRelativePath)
            .FirstOrDefault(IsMemoryTruthPath);
        if (string.IsNullOrWhiteSpace(protectedPath))
        {
            throw new InvalidOperationException("Worker direct memory write blocking requires at least one .ai/memory path.");
        }

        var violation = RuntimeProtectedTruthRootPolicyService.ClassifyViolation(protectedPath);
        var notes = attemptedPaths
            .Select(NormalizeRepoRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => $"attempted_path:{path}")
            .Append($"protected_classification:{violation.ProtectedClassification}")
            .Append(violation.RemediationAction)
            .ToArray();

        return PersistDecision(
            taskId: taskId,
            runId: runId,
            actor: "worker",
            entrypoint: "worker_patch",
            requestedEffect: "write_canonical_memory",
            decision: "block",
            reasonCode: "protected_truth_root_write",
            eventType: "memory.write.blocked",
            subject: new MemoryAdmissionSubject
            {
                Kind = "path",
                Id = protectedPath,
                Path = protectedPath,
            },
            target: protectedPath,
            notes: notes,
            evidenceRefs: [],
            declaredEffects: ["block_memory_truth_write"],
            observedEffects: ["block_memory_truth_write"],
            candidate: null,
            additionalOutputs: []);
    }

    public MemoryAdmissionDecisionResult StageWorkerClaimCandidate(
        string taskId,
        string runId,
        MemoryAdmissionClaimRequest request)
    {
        var candidate = memoryPromotionService.StageCandidate(
            request.Category,
            request.Title,
            request.Summary,
            request.Statement,
            request.Scope,
            proposer: "worker",
            sourceEvidenceIds: request.EvidenceRefs,
            confidence: request.Confidence,
            targetMemoryPath: request.TargetMemoryPath,
            taskScope: request.TaskScope,
            commitScope: request.CommitScope);
        var candidatePath = Path.Combine(paths.MemoryInboxRoot, $"{candidate.CandidateId}.json");

        var notes = new List<string>
        {
            $"candidate_id:{candidate.CandidateId}",
            "worker_claim_low_trust",
        };
        if (!string.IsNullOrWhiteSpace(request.TargetMemoryPath))
        {
            notes.Add($"target_memory_path:{NormalizeRepoRelativePath(request.TargetMemoryPath)}");
        }

        return PersistDecision(
            taskId: taskId,
            runId: runId,
            actor: "worker",
            entrypoint: "worker_claim",
            requestedEffect: "stage_candidate",
            decision: "candidate_only",
            reasonCode: "worker_claim_low_trust",
            eventType: "memory.claim.staged",
            subject: new MemoryAdmissionSubject
            {
                Kind = "claim",
                Id = request.ClaimId,
                Path = NormalizeOptionalPath(request.TargetMemoryPath),
                Scope = request.Scope,
            },
            target: request.ClaimId,
            notes: notes,
            evidenceRefs: request.EvidenceRefs,
            declaredEffects: ["stage_memory_candidate"],
            observedEffects: ["stage_memory_candidate"],
            candidate: candidate,
            additionalOutputs:
            [
                effectLedgerService.BuildOutput(
                    "memory_candidate",
                    effectLedgerService.ToRepoRelative(candidatePath),
                    effectLedgerService.HashFile(candidatePath)),
            ]);
    }

    public MemoryAdmissionDecisionResult BlockReviewApprovalCanonicalization(
        string taskId,
        string runId,
        MemoryAdmissionReviewRequest request)
    {
        var notes = new List<string>
        {
            "review approval does not canonicalize memory directly",
        };
        if (!string.IsNullOrWhiteSpace(request.TargetFactId))
        {
            notes.Add($"target_fact_id:{request.TargetFactId}");
        }

        return PersistDecision(
            taskId: taskId,
            runId: runId,
            actor: "review",
            entrypoint: "review_approval",
            requestedEffect: "promote_fact",
            decision: "block",
            reasonCode: "review_approval_not_promotion",
            eventType: "review.memory.no_canonical_write",
            subject: new MemoryAdmissionSubject
            {
                Kind = "review",
                Id = request.ReviewId,
                Scope = NormalizeOptionalPath(request.TargetMemoryPath),
            },
            target: request.TargetFactId ?? request.ReviewId,
            notes: notes,
            evidenceRefs: request.EvidenceRefs,
            declaredEffects: ["block_review_canonicalization"],
            observedEffects: ["block_review_canonicalization"],
            candidate: null,
            additionalOutputs: []);
    }

    public MemoryAdmissionDecisionResult StageHandoffClaimCandidate(
        string taskId,
        string runId,
        string handoffPacketId,
        MemoryAdmissionClaimRequest request)
    {
        var candidate = memoryPromotionService.StageCandidate(
            request.Category,
            request.Title,
            request.Summary,
            request.Statement,
            request.Scope,
            proposer: "handoff",
            sourceEvidenceIds: request.EvidenceRefs,
            confidence: request.Confidence,
            targetMemoryPath: request.TargetMemoryPath,
            taskScope: request.TaskScope,
            commitScope: request.CommitScope);
        var candidatePath = Path.Combine(paths.MemoryInboxRoot, $"{candidate.CandidateId}.json");

        var notes = new List<string>
        {
            $"candidate_id:{candidate.CandidateId}",
            $"handoff_packet_id:{handoffPacketId}",
            "handoff packets are continuity inputs and cannot canonicalize memory directly",
        };

        return PersistDecision(
            taskId: taskId,
            runId: runId,
            actor: "handoff",
            entrypoint: "handoff_claim",
            requestedEffect: "stage_candidate",
            decision: "candidate_only",
            reasonCode: "low_trust_continuity_packet",
            eventType: "memory.claim.staged",
            subject: new MemoryAdmissionSubject
            {
                Kind = "handoff_packet",
                Id = handoffPacketId,
                Path = NormalizeOptionalPath(request.TargetMemoryPath),
                Scope = request.Scope,
            },
            target: request.ClaimId,
            notes: notes,
            evidenceRefs: request.EvidenceRefs,
            declaredEffects: ["stage_memory_candidate"],
            observedEffects: ["stage_memory_candidate"],
            candidate: candidate,
            additionalOutputs:
            [
                effectLedgerService.BuildOutput(
                    "memory_candidate",
                    effectLedgerService.ToRepoRelative(candidatePath),
                    effectLedgerService.HashFile(candidatePath)),
            ]);
    }

    private MemoryAdmissionDecisionResult PersistDecision(
        string taskId,
        string runId,
        string actor,
        string entrypoint,
        string requestedEffect,
        string decision,
        string reasonCode,
        string eventType,
        MemoryAdmissionSubject subject,
        string target,
        IReadOnlyList<string> notes,
        IReadOnlyList<string> evidenceRefs,
        IReadOnlyList<string> declaredEffects,
        IReadOnlyList<string> observedEffects,
        MemoryPromotionCandidateRecord? candidate,
        IReadOnlyList<EffectLedgerOutput> additionalOutputs)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var certificateId = BuildOpaqueId("MEMADM");
        var stepId = eventType.Replace('.', '_');
        var ledgerPath = effectLedgerService.GetRunLedgerPath(runId);
        var telemetryEventId = PredictNextEventId(ledgerPath, $"EV-{runId}", stepId);
        var certificatePath = Path.Combine(GetRunCertificateDirectory(runId), $"{certificateId}.json");
        var eventPath = Path.Combine(GetRunEventDirectory(runId), $"{telemetryEventId}.json");

        var certificate = new MemoryAdmissionCertificateRecord
        {
            CertificateId = certificateId,
            TaskId = taskId,
            RunId = runId,
            Subject = subject,
            Actor = actor,
            Entrypoint = entrypoint,
            RequestedEffect = requestedEffect,
            Decision = decision,
            ReasonCode = reasonCode,
            PolicyVersion = DefaultPolicyVersion,
            EvidenceRefs = evidenceRefs.ToArray(),
            TelemetryEventId = telemetryEventId,
            WritebackAllowed = string.Equals(decision, "allow", StringComparison.Ordinal),
            Notes = notes.ToArray(),
            CreatedAt = createdAt,
        };
        WriteJson(certificatePath, certificate);

        var telemetryEvent = new MemoryGateEventRecord
        {
            EventId = telemetryEventId,
            EventType = eventType,
            SinkFamily = EffectLedgerSinkFamily,
            Actor = actor,
            Entrypoint = entrypoint,
            Target = target,
            Decision = decision,
            ReasonCode = reasonCode,
            PolicyVersion = DefaultPolicyVersion,
            TaskId = taskId,
            RunId = runId,
            CertificateId = certificateId,
            EvidenceRef = evidenceRefs.FirstOrDefault(),
            CreatedAt = createdAt,
        };
        WriteJson(eventPath, telemetryEvent);

        var outputs = new List<EffectLedgerOutput>
        {
            effectLedgerService.BuildOutput(
                "memory_gate_event",
                effectLedgerService.ToRepoRelative(eventPath),
                effectLedgerService.HashFile(eventPath)),
            effectLedgerService.BuildOutput(
                "memory_admission_certificate",
                effectLedgerService.ToRepoRelative(certificatePath),
                effectLedgerService.HashFile(certificatePath)),
        };
        outputs.AddRange(additionalOutputs);

        var append = effectLedgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                $"EV-{runId}",
                stepId,
                actor,
                declaredEffects,
                observedEffects,
                outputs,
                decision)
            {
                Schema = EventSchema,
                TaskId = taskId,
                RunId = runId,
                AdmissionState = decision,
                Facts = new Dictionary<string, string?>
                {
                    ["schema"] = EventSchema,
                    ["event_type"] = eventType,
                    ["sink_family"] = EffectLedgerSinkFamily,
                    ["actor"] = actor,
                    ["entrypoint"] = entrypoint,
                    ["target"] = target,
                    ["decision"] = decision,
                    ["reason_code"] = reasonCode,
                    ["policy_version"] = DefaultPolicyVersion,
                    ["certificate_id"] = certificateId,
                    ["evidence_ref"] = evidenceRefs.FirstOrDefault(),
                    ["created_at"] = createdAt.ToString("O"),
                    ["subject_kind"] = subject.Kind,
                    ["subject_id"] = subject.Id,
                    ["candidate_id"] = candidate?.CandidateId,
                },
            });
        if (!string.Equals(append.EventId, telemetryEventId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Predicted telemetry event id '{telemetryEventId}' did not match appended event '{append.EventId}'.");
        }

        return new MemoryAdmissionDecisionResult(
            decision,
            reasonCode,
            effectLedgerService.ToRepoRelative(certificatePath),
            effectLedgerService.ToRepoRelative(eventPath),
            effectLedgerService.ToRepoRelative(ledgerPath),
            telemetryEventId,
            certificate.WritebackAllowed,
            certificate,
            telemetryEvent,
            candidate);
    }

    private static bool IsMemoryTruthPath(string path)
    {
        return path.StartsWith(".ai/memory/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, ".ai/memory", StringComparison.OrdinalIgnoreCase);
    }

    private static string PredictNextEventId(string ledgerPath, string eventIdPrefix, string stepId)
    {
        var eventCount = 0;
        if (File.Exists(ledgerPath))
        {
            eventCount = File.ReadAllLines(ledgerPath)
                .Count(static line => !string.IsNullOrWhiteSpace(line));
        }

        var normalizedStep = new string(stepId
            .Select(static value => char.IsLetterOrDigit(value) ? char.ToUpperInvariant(value) : '-')
            .ToArray())
            .Trim('-');
        return $"{eventIdPrefix}-{normalizedStep}-{eventCount + 1:000}";
    }

    private static string BuildOpaqueId(string prefix)
    {
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }

    private static string NormalizeRepoRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : NormalizeRepoRelativePath(path);
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}

public sealed class MemoryAdmissionSubject
{
    public string Kind { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string? Scope { get; init; }
}

public sealed class MemoryAdmissionCertificateRecord
{
    public string Schema { get; init; } = MemoryAdmissionGateService.CertificateSchema;

    public string CertificateId { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public MemoryAdmissionSubject Subject { get; init; } = new();

    public string Actor { get; init; } = string.Empty;

    public string Entrypoint { get; init; } = string.Empty;

    public string RequestedEffect { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string PolicyVersion { get; init; } = MemoryAdmissionGateService.DefaultPolicyVersion;

    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];

    public string TelemetryEventId { get; init; } = string.Empty;

    public bool WritebackAllowed { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class MemoryGateEventRecord
{
    public string Schema { get; init; } = MemoryAdmissionGateService.EventSchema;

    public string EventId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public string SinkFamily { get; init; } = MemoryAdmissionGateService.EffectLedgerSinkFamily;

    public string Actor { get; init; } = string.Empty;

    public string Entrypoint { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string PolicyVersion { get; init; } = MemoryAdmissionGateService.DefaultPolicyVersion;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? CertificateId { get; init; }

    public string? EvidenceRef { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MemoryAdmissionClaimRequest(
    string ClaimId,
    string Category,
    string Title,
    string Summary,
    string Statement,
    string Scope,
    IReadOnlyList<string> EvidenceRefs)
{
    public string? TargetMemoryPath { get; init; }

    public string? TaskScope { get; init; }

    public string? CommitScope { get; init; }

    public double Confidence { get; init; } = 0.5;
}

public sealed record MemoryAdmissionReviewRequest(
    string ReviewId,
    IReadOnlyList<string> EvidenceRefs)
{
    public string? TargetFactId { get; init; }

    public string? TargetMemoryPath { get; init; }
}

public sealed record MemoryAdmissionDecisionResult(
    string Decision,
    string ReasonCode,
    string CertificatePath,
    string EventPath,
    string LedgerPath,
    string TelemetryEventId,
    bool WritebackAllowed,
    MemoryAdmissionCertificateRecord Certificate,
    MemoryGateEventRecord TelemetryEvent,
    MemoryPromotionCandidateRecord? Candidate);
