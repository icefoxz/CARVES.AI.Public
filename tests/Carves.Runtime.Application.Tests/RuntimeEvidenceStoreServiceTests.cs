using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeEvidenceStoreServiceTests
{
    [Fact]
    public void RecordReview_WritesAppendOnlyEvidenceWithLineage()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeEvidenceStoreService(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-001",
            CardId = "CARD-EVIDENCE",
            Title = "Persist review evidence",
            Description = "Capture review output as append-only evidence.",
            Status = DomainTaskStatus.Review,
        };
        var artifact = new PlannerReviewArtifact
        {
            TaskId = task.TaskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Acceptance met.",
                AcceptanceMet = true,
                BoundaryPreserved = true,
            },
            ResultingStatus = DomainTaskStatus.Completed,
            PlannerComment = "Ready for merge.",
            DecisionStatus = ReviewDecisionStatus.Approved,
            ValidationEvidence = [".ai/artifacts/reviews/T-EVIDENCE-001.json"],
        };

        var first = service.RecordReview(task, artifact, sourceEvidenceIds: ["RUNEVI-T-EVIDENCE-001-001"]);
        var second = service.RecordReview(task, artifact, sourceEvidenceIds: ["RUNEVI-T-EVIDENCE-001-001", "PLNEVI-T-EVIDENCE-001-001"]);
        var records = service.ListForTask(task.TaskId, RuntimeEvidenceKind.Review);

        Assert.Equal("raw_evidence", first.Tier);
        Assert.NotEqual(first.EvidenceId, second.EvidenceId);
        Assert.Equal(2, records.Count);
        Assert.Equal(second.EvidenceId, records[0].EvidenceId);
        Assert.Contains("RUNEVI-T-EVIDENCE-001-001", records[0].SourceEvidenceIds);
        Assert.Contains("PLNEVI-T-EVIDENCE-001-001", records[0].SourceEvidenceIds);
        Assert.Contains(".ai/artifacts/reviews/T-EVIDENCE-001.json", records[0].ArtifactPaths);
    }

    [Fact]
    public void Search_ReturnsBudgetBoundedResultsWithTopSources()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeEvidenceStoreService(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-SEARCH-001",
            CardId = "CARD-EVIDENCE",
            Title = "Search evidence",
            Description = "Keep evidence search bounded and provenance-bearing.",
            Status = DomainTaskStatus.Pending,
        };

        var first = service.RecordPlanning(
            task,
            "planner",
            "Schema gate reasoning for evidence search budget " + new string('a', 240),
            [".ai/runtime/context-packs/tasks/T-EVIDENCE-SEARCH-001.json"]);
        var second = service.RecordPlanning(
            task,
            "planner",
            "Schema gate follow-up with broader rationale " + new string('b', 240),
            [".ai/evidence/excerpts/planning/tasks/T-EVIDENCE-SEARCH-001/PLNEVI-T-EVIDENCE-SEARCH-001-001.json"]);
        var budget = ContextBudgetPolicyResolver.EstimateTokens($"{second.Summary} {second.Excerpt}") + 5;

        var result = service.Search("schema gate", task.TaskId, RuntimeEvidenceKind.Planning, budgetTokens: budget, take: 5);

        Assert.Single(result.Records);
        Assert.True(result.UsedTokens <= result.BudgetTokens);
        Assert.True(result.DroppedRecords > 0);
        Assert.NotEmpty(result.TopSources);
        Assert.All(result.Records, item => Assert.Equal(RuntimeEvidenceKind.Planning, item.Kind));
        Assert.Equal(second.EvidenceId, result.Records[0].EvidenceId);
    }
}
