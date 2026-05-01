using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class ContinuityHandoffInspectionServiceTests
{
    [Fact]
    public void Inspect_ValidPacketReturnsReady()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket());

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("carves-continuity-handoff-inspection.v1", result.SchemaVersion);
        Assert.Equal("usable", result.InspectionStatus);
        Assert.Equal("ready", result.Readiness.Decision);
        Assert.Equal("HND-TEST-001", result.HandoffId);
        Assert.Equal("ready", result.ResumeStatus);
        Assert.Equal("medium", result.Confidence);
        Assert.Single(result.ContextRefs);
        Assert.Single(result.EvidenceRefs);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Inspect_MissingPacketFailsClosed()
    {
        using var workspace = new TemporaryWorkspace();

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "missing.json");

        Assert.Equal("missing", result.InspectionStatus);
        Assert.Equal("invalid", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "packet.missing");
    }

    [Fact]
    public void Inspect_MalformedJsonFailsClosed()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", "{ malformed");

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("malformed", result.InspectionStatus);
        Assert.Equal("invalid", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "packet.malformed");
    }

    [Fact]
    public void Inspect_CompletedFactWithoutEvidenceRequiresOperatorReview()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket().Replace("""
              "evidence_refs": [
                "docs/runtime/example.md"
              ],
        """, """
              "evidence_refs": [],
        """, StringComparison.Ordinal));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("missing_evidence", result.InspectionStatus);
        Assert.Equal("operator_review_required", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "completed_facts.missing_evidence_refs");
    }

    [Fact]
    public void Inspect_LowConfidencePacketRequiresReorientation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket().Replace(
            "\"confidence\": \"medium\"",
            "\"confidence\": \"low\"",
            StringComparison.Ordinal));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("low_confidence", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
    }

    [Fact]
    public void Inspect_DoneNoNextActionPacketReturnsTerminalDoneReadiness()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket().Replace(
            "\"resume_status\": \"ready\"",
            "\"resume_status\": \"done_no_next_action\"",
            StringComparison.Ordinal));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("done", result.InspectionStatus);
        Assert.Equal("done", result.Readiness.Decision);
        Assert.Equal("done_no_next_action", result.ResumeStatus);
        Assert.Contains("no next action", result.Readiness.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_OldPacketRequiresFreshnessReorientation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket(
            DateTimeOffset.UtcNow.AddDays(-(HandoffInspectionService.DefaultFreshnessThresholdDays + 1)).ToString("O")));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "packet.age_stale");
    }

    [Fact]
    public void Inspect_MustNotRepeatSupportsStringAndTextObjectForms()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacket(mustNotRepeatJson: """
          "must_not_repeat": [
            "Do not repeat setup.",
            {
              "text": "Do not reopen the closed investigation.",
              "reason": "The packet already records the result."
            }
          ],
        """));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("ready", result.Readiness.Decision);
        Assert.Equal(2, result.MustNotRepeat.Count);
        Assert.Contains(result.MustNotRepeat, item => item.Text == "Do not repeat setup.");
        Assert.Contains(result.MustNotRepeat, item => item.Text == "Do not reopen the closed investigation.");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("must_not_repeat.", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_BranchMismatchRequiresReorientation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacketWithRepo("feature/handoff", "abc123", "clean"));

        var result = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot("codex/dev", "abc123", "clean", Available: true, null)))
            .Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "repo.branch_mismatch");
    }

    [Fact]
    public void Inspect_BaseCommitMismatchRequiresReorientation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacketWithRepo("codex/dev", "abc123", "clean"));

        var result = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot("codex/dev", "def456", "clean", Available: true, null)))
            .Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "repo.base_commit_mismatch");
    }

    [Fact]
    public void Inspect_DirtyStateMismatchRequiresReorientation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacketWithRepo("codex/dev", "abc123", "clean"));

        var result = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot("codex/dev", "abc123", "dirty", Available: true, null)))
            .Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "repo.dirty_state_mismatch");
    }

    [Fact]
    public void Inspect_UnknownPacketOrientationDoesNotBlockReadyPacket()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacketWithRepo("unknown", "unknown", "unknown"));

        var result = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot("codex/dev", "abc123", "dirty", Available: true, null)))
            .Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("usable", result.InspectionStatus);
        Assert.Equal("ready", result.Readiness.Decision);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("repo.", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_ConcretePacketOrientationFailsClosedWhenCurrentOrientationUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff.json", ValidPacketWithRepo("codex/dev", "abc123", "dirty"));

        var result = new HandoffInspectionService(new FakeOrientationReader(
            new HandoffRepoOrientationSnapshot(null, null, "unknown", Available: false, "git unavailable")))
            .Inspect(workspace.RootPath, "handoff.json");

        Assert.Equal("stale", result.InspectionStatus);
        Assert.Equal("reorient_first", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "repo.orientation_unavailable");
    }

    private static string ValidPacket(string? createdAtUtc = null, string? mustNotRepeatJson = null)
    {
        createdAtUtc ??= DateTimeOffset.UtcNow.ToString("O");
        mustNotRepeatJson ??= """
          "must_not_repeat": [
            {
              "item": "Do not create default storage.",
              "reason": "Phase 3 is read-only."
            }
          ],
        """;

        return $$"""
        {
          "schema_version": "carves-continuity-handoff.v1",
          "handoff_id": "HND-TEST-001",
          "created_at_utc": "{{createdAtUtc}}",
          "producer": {
            "agent": "test"
          },
          "repo": {
            "name": "CARVES.Runtime"
          },
          "resume_status": "ready",
          "current_objective": "Test incoming handoff inspection.",
          "current_cursor": {
            "kind": "manual",
            "id": "TEST"
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
        {{mustNotRepeatJson}}
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
          "confidence": "medium",
          "confidence_notes": [
            "Test fixture."
          ]
        }
        """;
    }

    private static string ValidPacketWithRepo(string branch, string baseCommit, string dirtyState)
    {
        return ValidPacket().Replace("""
          "repo": {
            "name": "CARVES.Runtime"
          },
        """, $$"""
          "repo": {
            "name": "CARVES.Runtime",
            "branch": "{{branch}}",
            "base_commit": "{{baseCommit}}",
            "dirty_state": "{{dirtyState}}"
          },
        """, StringComparison.Ordinal);
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
