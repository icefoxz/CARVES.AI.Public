using Carves.Runtime.Application.Memory;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeMemoryPromotionServiceTests
{
    [Fact]
    public void PromoteCandidateToProvisional_WritesCandidateAuditPromotionAndActiveFact()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeMemoryPromotionService(workspace.Paths);

        var candidate = service.StageCandidate(
            category: "project",
            title: "Current budget policy is single-model.",
            summary: "Record the active single-model budget posture.",
            statement: "CARVES token budgeting currently executes on a single-model context-pack spine.",
            scope: "repo:CARVES.Runtime",
            proposer: "ContextPackService",
            sourceEvidenceIds: ["CTXEVI-T-CARD-693-001-001", "PLNEVI-T-CARD-693-001-001"],
            confidence: 0.86,
            targetMemoryPath: ".ai/memory/project/runtime-token-budget.md",
            taskScope: "T-CARD-693-001",
            commitScope: "abc123");
        var audit = service.RecordCandidateAudit(candidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Evidence chain is sufficient.");

        var fact = service.PromoteCandidateToProvisional(candidate.CandidateId, audit.AuditId, "memory-review");
        var promotions = service.ListPromotionRecords();

        Assert.Equal(MemoryKnowledgeTier.ProvisionalMemory, fact.Tier);
        Assert.Equal(TemporalMemoryFactStatus.Active, fact.Status);
        Assert.Equal(candidate.CandidateId, fact.SourceCandidateId);
        Assert.Contains("CTXEVI-T-CARD-693-001-001", fact.SourceEvidenceIds);
        Assert.Equal(".ai/memory/project/runtime-token-budget.md", fact.TargetMemoryPath);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.MemoryInboxRoot, $"{candidate.CandidateId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.MemoryAuditsRoot, $"{audit.AuditId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.EvidenceFactsRoot, $"{fact.FactId}.json")));
        Assert.Contains(promotions, item => item.Action == MemoryPromotionAction.PromoteToProvisional && item.ResultFactId == fact.FactId);
    }

    [Fact]
    public void PromoteFactToCanonical_SupersedesPriorFactAndKeepsHistory()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeMemoryPromotionService(workspace.Paths);

        var candidate = service.StageCandidate(
            category: "project",
            title: "Evidence facts stay outside canonical memory by default.",
            summary: "Capture the default evidence/fact boundary.",
            statement: "Raw evidence remains outside canonical memory until promotion completes.",
            scope: "repo:CARVES.Runtime",
            proposer: "RuntimeEvidenceStoreService",
            sourceEvidenceIds: ["RUNEVI-T-CARD-693-002-001"],
            confidence: 0.91);
        var candidateAudit = service.RecordCandidateAudit(candidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Promote to provisional first.");
        var provisional = service.PromoteCandidateToProvisional(candidate.CandidateId, candidateAudit.AuditId, "memory-review");
        var canonicalAudit = service.RecordFactAudit(provisional.FactId, MemoryPromotionAuditDecision.Approved, "review-task", "Promote the fact to canonical.");

        var canonical = service.PromoteFactToCanonical(provisional.FactId, canonicalAudit.AuditId, "memory-review");
        var facts = service.ListFacts(scope: "repo:CARVES.Runtime", take: 10);
        var superseded = facts.Single(item => item.FactId == provisional.FactId);

        Assert.Equal(MemoryKnowledgeTier.CanonicalMemory, canonical.Tier);
        Assert.Equal(TemporalMemoryFactStatus.Active, canonical.Status);
        Assert.Equal(provisional.FactId, canonical.SourceFactId);
        Assert.Contains(provisional.FactId, canonical.Supersedes);
        Assert.Equal(TemporalMemoryFactStatus.Superseded, superseded.Status);
        Assert.NotNull(superseded.ValidToUtc);
        Assert.Equal(canonical.FactId, superseded.SupersededByFactId);
        Assert.Contains(service.ListPromotionRecords(), item => item.Action == MemoryPromotionAction.PromoteToCanonical && item.ResultFactId == canonical.FactId);
    }

    [Fact]
    public void InvalidateFact_ClosesValidityWindowWithoutDeletingPriorFact()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeMemoryPromotionService(workspace.Paths);

        var candidate = service.StageCandidate(
            category: "module",
            title: "Facet narrowing happens before semantic recall.",
            summary: "Record the retrieval ordering boundary.",
            statement: "Facet narrowing runs before semantic recall expansion in context assembly.",
            scope: "module:ContextPackService",
            proposer: "ContextPackService",
            sourceEvidenceIds: ["CTXEVI-T-CARD-694-001-001"],
            confidence: 0.88);
        var candidateAudit = service.RecordCandidateAudit(candidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Stage the claim.");
        var provisional = service.PromoteCandidateToProvisional(candidate.CandidateId, candidateAudit.AuditId, "memory-review");
        var canonicalAudit = service.RecordFactAudit(provisional.FactId, MemoryPromotionAuditDecision.Approved, "review-task", "Promote to canonical.");
        var canonical = service.PromoteFactToCanonical(provisional.FactId, canonicalAudit.AuditId, "memory-review");
        var invalidationAudit = service.RecordFactAudit(canonical.FactId, MemoryPromotionAuditDecision.Approved, "review-task", "Newer retrieval evidence invalidates this claim.");

        var invalidated = service.InvalidateFact(canonical.FactId, invalidationAudit.AuditId, "memory-review", "Superseded by a newer retrieval contract.");
        var activeFacts = service.ListActiveFacts(scope: "module:ContextPackService");

        Assert.Equal(TemporalMemoryFactStatus.Invalidated, invalidated.Status);
        Assert.NotNull(invalidated.ValidToUtc);
        Assert.Equal(invalidationAudit.AuditId, invalidated.InvalidatedByAuditId);
        Assert.Equal("Superseded by a newer retrieval contract.", invalidated.InvalidatedReason);
        Assert.DoesNotContain(activeFacts, item => item.FactId == canonical.FactId);
        Assert.Contains(service.ListPromotionRecords(), item => item.Action == MemoryPromotionAction.Invalidate && item.ResultFactId == canonical.FactId);
    }
}
