using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Memory;

public sealed class RuntimeMemoryPromotionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;

    public RuntimeMemoryPromotionService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public MemoryPromotionCandidateRecord StageCandidate(
        string category,
        string title,
        string summary,
        string statement,
        string scope,
        string proposer,
        IReadOnlyList<string> sourceEvidenceIds,
        double confidence,
        string? targetMemoryPath = null,
        string? taskScope = null,
        string? commitScope = null)
    {
        Directory.CreateDirectory(paths.MemoryInboxRoot);
        var candidate = new MemoryPromotionCandidateRecord
        {
            CandidateId = CreateSequentialId("MEMCAND", paths.MemoryInboxRoot),
            Category = category,
            Title = title,
            Summary = summary,
            Statement = statement,
            Scope = scope,
            TaskScope = taskScope,
            CommitScope = commitScope,
            TargetMemoryPath = NormalizeRepoRelativePath(targetMemoryPath),
            SourceEvidenceIds = DistinctValues(sourceEvidenceIds),
            Proposer = proposer,
            Confidence = confidence,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetCandidatePath(candidate.CandidateId), candidate);
        return candidate;
    }

    public MemoryPromotionAuditRecord RecordCandidateAudit(
        string candidateId,
        MemoryPromotionAuditDecision decision,
        string auditor,
        string rationale)
    {
        _ = LoadCandidate(candidateId);
        Directory.CreateDirectory(paths.MemoryAuditsRoot);
        var audit = new MemoryPromotionAuditRecord
        {
            AuditId = CreateSequentialId("MEMAUD", paths.MemoryAuditsRoot),
            CandidateId = candidateId,
            Decision = decision,
            Auditor = auditor,
            Rationale = rationale,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetAuditPath(audit.AuditId), audit);
        return audit;
    }

    public MemoryPromotionAuditRecord RecordFactAudit(
        string factId,
        MemoryPromotionAuditDecision decision,
        string auditor,
        string rationale)
    {
        _ = LoadFact(factId);
        Directory.CreateDirectory(paths.MemoryAuditsRoot);
        var audit = new MemoryPromotionAuditRecord
        {
            AuditId = CreateSequentialId("MEMAUD", paths.MemoryAuditsRoot),
            FactId = factId,
            Decision = decision,
            Auditor = auditor,
            Rationale = rationale,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetAuditPath(audit.AuditId), audit);
        return audit;
    }

    public TemporalMemoryFactRecord PromoteCandidateToProvisional(
        string candidateId,
        string auditId,
        string promotedBy)
    {
        var candidate = LoadCandidate(candidateId);
        var audit = LoadAudit(auditId);
        EnsureApprovedAudit(audit, candidateId: candidateId, factId: null);

        Directory.CreateDirectory(paths.EvidenceFactsRoot);
        Directory.CreateDirectory(paths.MemoryPromotionsRoot);
        var promotionId = CreateSequentialId("MEMPROM", paths.MemoryPromotionsRoot);
        var fact = new TemporalMemoryFactRecord
        {
            FactId = CreateSequentialId("MEMFACT", paths.EvidenceFactsRoot),
            Tier = MemoryKnowledgeTier.ProvisionalMemory,
            Status = TemporalMemoryFactStatus.Active,
            Category = candidate.Category,
            Title = candidate.Title,
            Summary = candidate.Summary,
            Statement = candidate.Statement,
            Scope = candidate.Scope,
            TaskScope = candidate.TaskScope,
            CommitScope = candidate.CommitScope,
            TargetMemoryPath = candidate.TargetMemoryPath,
            SourceEvidenceIds = candidate.SourceEvidenceIds,
            SourceCandidateId = candidate.CandidateId,
            PromotionRecordId = promotionId,
            ProposedBy = candidate.Proposer,
            PromotedBy = promotedBy,
            Confidence = candidate.Confidence,
            ValidFromUtc = DateTimeOffset.UtcNow,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        var promotion = new MemoryPromotionRecord
        {
            PromotionId = promotionId,
            Action = MemoryPromotionAction.PromoteToProvisional,
            CandidateId = candidate.CandidateId,
            AuditId = audit.AuditId,
            ResultFactId = fact.FactId,
            ResultTier = fact.Tier,
            Summary = $"Candidate {candidate.CandidateId} promoted to provisional memory fact {fact.FactId}.",
            Actor = promotedBy,
            RecordedAtUtc = fact.RecordedAtUtc,
        };
        WriteJson(GetFactPath(fact.FactId), fact);
        WriteJson(GetPromotionPath(promotion.PromotionId), promotion);
        return fact;
    }

    public TemporalMemoryFactRecord PromoteFactToCanonical(
        string factId,
        string auditId,
        string promotedBy,
        IReadOnlyList<string>? supersedes = null)
    {
        var source = LoadFact(factId);
        var audit = LoadAudit(auditId);
        EnsureApprovedAudit(audit, candidateId: null, factId: factId);

        Directory.CreateDirectory(paths.EvidenceFactsRoot);
        Directory.CreateDirectory(paths.MemoryPromotionsRoot);
        var now = DateTimeOffset.UtcNow;
        var supersededIds = new List<string> { factId };
        if (supersedes is not null)
        {
            supersededIds.AddRange(supersedes.Where(static item => !string.IsNullOrWhiteSpace(item)));
        }

        var distinctSupersededIds = supersededIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var promotionId = CreateSequentialId("MEMPROM", paths.MemoryPromotionsRoot);
        var canonical = source with
        {
            FactId = CreateSequentialId("MEMFACT", paths.EvidenceFactsRoot),
            Tier = MemoryKnowledgeTier.CanonicalMemory,
            Status = TemporalMemoryFactStatus.Active,
            SourceFactId = source.FactId,
            PromotionRecordId = promotionId,
            PromotedBy = promotedBy,
            Supersedes = distinctSupersededIds,
            SupersededByFactId = null,
            InvalidatedByAuditId = null,
            InvalidatedReason = null,
            ValidFromUtc = now,
            ValidToUtc = null,
            RecordedAtUtc = now,
        };

        foreach (var supersededId in distinctSupersededIds)
        {
            var supersededFact = LoadFact(supersededId);
            if (supersededFact.Status != TemporalMemoryFactStatus.Active)
            {
                continue;
            }

            WriteJson(
                GetFactPath(supersededId),
                supersededFact with
                {
                    Status = TemporalMemoryFactStatus.Superseded,
                    ValidToUtc = now,
                    SupersededByFactId = canonical.FactId,
                });
        }

        var promotion = new MemoryPromotionRecord
        {
            PromotionId = promotionId,
            Action = MemoryPromotionAction.PromoteToCanonical,
            SourceFactId = source.FactId,
            AuditId = audit.AuditId,
            ResultFactId = canonical.FactId,
            ResultTier = canonical.Tier,
            Summary = $"Fact {source.FactId} promoted to canonical memory fact {canonical.FactId}.",
            Actor = promotedBy,
            Supersedes = distinctSupersededIds,
            RecordedAtUtc = now,
        };
        WriteJson(GetFactPath(canonical.FactId), canonical);
        WriteJson(GetPromotionPath(promotion.PromotionId), promotion);
        return canonical;
    }

    public TemporalMemoryFactRecord InvalidateFact(
        string factId,
        string auditId,
        string invalidatedBy,
        string reason)
    {
        var fact = LoadFact(factId);
        var audit = LoadAudit(auditId);
        EnsureApprovedAudit(audit, candidateId: null, factId: factId);
        if (fact.Status != TemporalMemoryFactStatus.Active)
        {
            throw new InvalidOperationException($"Fact '{factId}' is already {fact.Status.ToString().ToLowerInvariant()}.");
        }

        Directory.CreateDirectory(paths.MemoryPromotionsRoot);
        var now = DateTimeOffset.UtcNow;
        var invalidated = fact with
        {
            Status = TemporalMemoryFactStatus.Invalidated,
            ValidToUtc = now,
            InvalidatedByAuditId = audit.AuditId,
            InvalidatedReason = reason,
            PromotedBy = invalidatedBy,
        };
        var promotion = new MemoryPromotionRecord
        {
            PromotionId = CreateSequentialId("MEMPROM", paths.MemoryPromotionsRoot),
            Action = MemoryPromotionAction.Invalidate,
            SourceFactId = fact.FactId,
            AuditId = audit.AuditId,
            ResultFactId = fact.FactId,
            ResultTier = fact.Tier,
            Summary = $"Fact {fact.FactId} invalidated after audit {audit.AuditId}.",
            Actor = invalidatedBy,
            RecordedAtUtc = now,
        };
        WriteJson(GetFactPath(factId), invalidated);
        WriteJson(GetPromotionPath(promotion.PromotionId), promotion);
        return invalidated;
    }

    public IReadOnlyList<TemporalMemoryFactRecord> ListFacts(
        string? scope = null,
        MemoryKnowledgeTier? tier = null,
        TemporalMemoryFactStatus? status = null,
        int take = 100)
    {
        if (!Directory.Exists(paths.EvidenceFactsRoot))
        {
            return Array.Empty<TemporalMemoryFactRecord>();
        }

        return Directory.EnumerateFiles(paths.EvidenceFactsRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadFact)
            .Where(item => string.IsNullOrWhiteSpace(scope)
                           || string.Equals(item.Scope, scope, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(item.TaskScope, scope, StringComparison.OrdinalIgnoreCase))
            .Where(item => tier is null || item.Tier == tier.Value)
            .Where(item => status is null || item.Status == status.Value)
            .OrderByDescending(item => item.ValidFromUtc)
            .ThenByDescending(item => item.FactId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    public IReadOnlyList<TemporalMemoryFactRecord> ListActiveFacts(string? scope = null, int take = 100)
    {
        return ListFacts(scope, tier: null, status: TemporalMemoryFactStatus.Active, take);
    }

    public IReadOnlyList<MemoryPromotionRecord> ListPromotionRecords(int take = 100)
    {
        if (!Directory.Exists(paths.MemoryPromotionsRoot))
        {
            return Array.Empty<MemoryPromotionRecord>();
        }

        return Directory.EnumerateFiles(paths.MemoryPromotionsRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadPromotion)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.PromotionId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    private MemoryPromotionCandidateRecord LoadCandidate(string candidateId)
    {
        var path = GetCandidatePath(candidateId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Memory candidate '{candidateId}' was not found.");
        }

        return JsonSerializer.Deserialize<MemoryPromotionCandidateRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Memory candidate '{candidateId}' could not be deserialized.");
    }

    private MemoryPromotionAuditRecord LoadAudit(string auditId)
    {
        var path = GetAuditPath(auditId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Memory audit '{auditId}' was not found.");
        }

        return JsonSerializer.Deserialize<MemoryPromotionAuditRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Memory audit '{auditId}' could not be deserialized.");
    }

    private TemporalMemoryFactRecord LoadFact(string factId)
    {
        var path = GetFactPath(factId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Memory fact '{factId}' was not found.");
        }

        return JsonSerializer.Deserialize<TemporalMemoryFactRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Memory fact '{factId}' could not be deserialized.");
    }

    private MemoryPromotionRecord ReadPromotion(string path)
    {
        return JsonSerializer.Deserialize<MemoryPromotionRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Memory promotion record at '{path}' could not be deserialized.");
    }

    private TemporalMemoryFactRecord ReadFact(string path)
    {
        return JsonSerializer.Deserialize<TemporalMemoryFactRecord>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Memory fact at '{path}' could not be deserialized.");
    }

    private void EnsureApprovedAudit(MemoryPromotionAuditRecord audit, string? candidateId, string? factId)
    {
        if (audit.Decision != MemoryPromotionAuditDecision.Approved)
        {
            throw new InvalidOperationException($"Audit '{audit.AuditId}' is not approved.");
        }

        if (!string.IsNullOrWhiteSpace(candidateId) && !string.Equals(audit.CandidateId, candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Audit '{audit.AuditId}' does not match candidate '{candidateId}'.");
        }

        if (!string.IsNullOrWhiteSpace(factId) && !string.Equals(audit.FactId, factId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Audit '{audit.AuditId}' does not match fact '{factId}'.");
        }
    }

    private string GetCandidatePath(string candidateId)
    {
        return Path.Combine(paths.MemoryInboxRoot, $"{candidateId}.json");
    }

    private string GetAuditPath(string auditId)
    {
        return Path.Combine(paths.MemoryAuditsRoot, $"{auditId}.json");
    }

    private string GetFactPath(string factId)
    {
        return Path.Combine(paths.EvidenceFactsRoot, $"{factId}.json");
    }

    private string GetPromotionPath(string promotionId)
    {
        return Path.Combine(paths.MemoryPromotionsRoot, $"{promotionId}.json");
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static string CreateSequentialId(string prefix, string root)
    {
        Directory.CreateDirectory(root);
        var next = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Count() + 1;
        return $"{prefix}-{next:000}";
    }

    private string? NormalizeRepoRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Path.IsPathRooted(value))
        {
            return value.Replace('\\', '/');
        }

        var relative = Path.GetRelativePath(paths.RepoRoot, value).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal)
            ? value.Replace('\\', '/')
            : relative;
    }

    private static IReadOnlyList<string> DistinctValues(IEnumerable<string> values)
    {
        return values
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
