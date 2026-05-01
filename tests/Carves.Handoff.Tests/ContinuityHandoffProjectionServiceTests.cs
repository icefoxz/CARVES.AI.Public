using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class ContinuityHandoffProjectionServiceTests
{
    [Fact]
    public void Project_ReadyPacketReturnsContinueAction()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket("ready", "medium"));

        var result = new HandoffProjectionService().Project(workspace.RootPath, "handoff.json");

        Assert.Equal("carves-continuity-handoff-next.v1", result.SchemaVersion);
        Assert.Equal("continue", result.Action);
        Assert.Equal("ready", result.Readiness.Decision);
        Assert.Equal("Test projection objective.", result.CurrentObjective);
        Assert.NotNull(result.CurrentCursor);
        Assert.Equal("TEST", result.CurrentCursor.Id);
        Assert.Single(result.ContextRefs);
        Assert.Single(result.EvidenceRefs);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Project_OperatorReviewPacketPreservesNonReadyState()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket("operator_review_required", "low"));

        var result = new HandoffProjectionService().Project(workspace.RootPath, "handoff.json");

        Assert.Equal("operator_review_first", result.Action);
        Assert.Equal("operator_review_required", result.Readiness.Decision);
        Assert.Equal("operator_review_required", result.ResumeStatus);
        Assert.Equal("low", result.Confidence);
    }

    [Fact]
    public void Project_DoneNoNextActionPacketReturnsNoAction()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket("done_no_next_action", "medium"));

        var result = new HandoffProjectionService().Project(workspace.RootPath, "handoff.json");

        Assert.Equal("no_action", result.Action);
        Assert.Equal("done", result.InspectionStatus);
        Assert.Equal("done", result.Readiness.Decision);
        Assert.Equal("done_no_next_action", result.ResumeStatus);
        Assert.DoesNotContain("continue", result.Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_MissingPacketReturnsInvalidAction()
    {
        using var workspace = new TemporaryWorkspace();

        var result = new HandoffProjectionService().Project(workspace.RootPath, "missing.json");

        Assert.Equal("invalid", result.Action);
        Assert.Equal("missing", result.InspectionStatus);
        Assert.Equal("invalid", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "packet.missing");
    }

    [Fact]
    public void Project_StalePacketReturnsReorientFirstAction()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket("ready", "medium").Replace("""
          "repo": {
            "name": "CARVES.Runtime"
          },
        """, """
          "repo": {
            "name": "CARVES.Runtime",
            "branch": "feature/stale",
            "base_commit": "abc123",
            "dirty_state": "clean"
          },
        """, StringComparison.Ordinal));

        var inspection = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot("codex/dev", "abc123", "clean", Available: true, null)));
        var result = new HandoffProjectionService(inspection).Project(workspace.RootPath, "handoff.json");

        Assert.Equal("reorient_first", result.Action);
        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "repo.branch_mismatch");
    }

    private static string ValidPacket(string resumeStatus, string confidence)
    {
        return $$"""
        {
          "schema_version": "carves-continuity-handoff.v1",
          "handoff_id": "HND-NEXT-001",
          "created_at_utc": "2026-04-14T00:00:00Z",
          "producer": {
            "agent": "test"
          },
          "repo": {
            "name": "CARVES.Runtime"
          },
          "resume_status": "{{resumeStatus}}",
          "current_objective": "Test projection objective.",
          "current_cursor": {
            "kind": "manual",
            "id": "TEST",
            "title": "Projection test",
            "status": "in_progress",
            "scope": [
              "docs/runtime/example.md"
            ]
          },
          "completed_facts": [
            {
              "statement": "A bounded handoff packet exists.",
              "evidence_refs": [
                "docs/runtime/example.md"
              ],
              "confidence": "high"
            }
          ],
          "remaining_work": [
            {
              "action": "Inspect the packet."
            }
          ],
          "blocked_reasons": [],
          "must_not_repeat": [
            {
              "item": "Do not create default storage.",
              "reason": "Projection is read-only."
            }
          ],
          "open_questions": [],
          "decision_refs": [],
          "evidence_refs": [
            {
              "kind": "doc",
              "ref": "docs/runtime/example.md",
              "summary": "Example evidence."
            }
          ],
          "context_refs": [
            {
              "ref": "docs/runtime/example.md",
              "reason": "Provides bounded context.",
              "priority": 1
            }
          ],
          "recommended_next_action": {
            "action": "Read the listed context refs.",
            "rationale": "Avoid broad rediscovery."
          },
          "confidence": "{{confidence}}",
          "confidence_notes": [
            "Test fixture."
          ]
        }
        """;
    }

    private sealed class FakeOrientationReader : IHandoffRepoOrientationReader
    {
        private readonly HandoffRepoOrientationSnapshot snapshot;

        public FakeOrientationReader(HandoffRepoOrientationSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public HandoffRepoOrientationSnapshot Read(string repoRoot)
        {
            return snapshot;
        }
    }
}
