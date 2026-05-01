using Carves.Runtime.Application.Memory;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Tests;

public sealed class ActiveMemoryReadServiceTests
{
    [Fact]
    public void LoadProjectDocumentsWithProjectedFacts_KeepsLegacyDocsAndExcludesInvalidatedFactsByDefault()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/memory/project/runtime_context.md", """
# Runtime Context

Legacy markdown memory stays readable during Batch A retrofit.
""");

        var repoScope = $"repo:{Path.GetFileName(workspace.RootPath)}";
        var promotionService = new RuntimeMemoryPromotionService(workspace.Paths);

        var activeCandidate = promotionService.StageCandidate(
            category: "project",
            title: "Active memory filter excludes invalidated facts.",
            summary: "Capture the current active read rule.",
            statement: "Active memory filter excludes invalidated facts from context assembly.",
            scope: repoScope,
            proposer: "test",
            sourceEvidenceIds: ["EVI-ACTIVE-001"],
            confidence: 0.94);
        var activeAudit = promotionService.RecordCandidateAudit(activeCandidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Active fact is supported.");
        var activeFact = promotionService.PromoteCandidateToProvisional(activeCandidate.CandidateId, activeAudit.AuditId, "memory-review");

        var staleCandidate = promotionService.StageCandidate(
            category: "project",
            title: "Old memory filter allowed invalidated facts.",
            summary: "Capture a stale rule for invalidation coverage.",
            statement: "Old context assembly allowed invalidated facts into active reads.",
            scope: repoScope,
            proposer: "test",
            sourceEvidenceIds: ["EVI-STALE-001"],
            confidence: 0.73);
        var staleAudit = promotionService.RecordCandidateAudit(staleCandidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Stage stale fact.");
        var staleFact = promotionService.PromoteCandidateToProvisional(staleCandidate.CandidateId, staleAudit.AuditId, "memory-review");
        var invalidateAudit = promotionService.RecordFactAudit(staleFact.FactId, MemoryPromotionAuditDecision.Approved, "review-task", "Invalidate stale fact.");
        var invalidatedFact = promotionService.InvalidateFact(staleFact.FactId, invalidateAudit.AuditId, "memory-review", "No longer valid.");

        var service = new ActiveMemoryReadService(workspace.Paths);

        var documents = service.LoadProjectDocumentsWithProjectedFacts([repoScope], take: 20);
        var activeFacts = service.ListFacts([repoScope], includeInactiveFacts: false, take: 20);
        var allFacts = service.ListFacts([repoScope], includeInactiveFacts: true, take: 20);

        Assert.Contains(documents, item => item.Path == ".ai/memory/project/runtime_context.md");
        Assert.Contains(documents, item => item.Path == $".ai/evidence/facts/{activeFact.FactId}.json");
        Assert.DoesNotContain(documents, item => item.Path == $".ai/evidence/facts/{invalidatedFact.FactId}.json");
        Assert.Contains(activeFacts, item => item.FactId == activeFact.FactId);
        Assert.DoesNotContain(activeFacts, item => item.FactId == invalidatedFact.FactId);
        Assert.Contains(allFacts, item => item.FactId == invalidatedFact.FactId);
    }
}
