using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class HandoffGuardDecisionReferenceTests
{
    [Fact]
    public void Inspect_LinksGuardDecisionReferenceWhenDecisionExists()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", "{\"schema_version\":1,\"run_id\":\"GRD-123\",\"outcome\":\"allow\"}\n");
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket(decisionRefs: """
        [
          "guard-run:GRD-123"
        ]
        """));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("ready", result.Readiness.Decision);
        var reference = Assert.Single(result.DecisionRefs);
        Assert.Equal("linked", reference.Status);
        Assert.Equal("GRD-123", reference.MatchedRunId);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "decision_refs.guard_unresolved");
    }

    [Fact]
    public void Inspect_UnresolvedGuardDecisionReferenceWarnsWithoutBlocking()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", "{\"schema_version\":1,\"run_id\":\"GRD-123\",\"outcome\":\"allow\"}\n");
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket(decisionRefs: """
        [
          { "kind": "guard", "ref": "GRD-MISSING", "summary": "Expected missing ref." }
        ]
        """));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("ready", result.Readiness.Decision);
        var reference = Assert.Single(result.DecisionRefs);
        Assert.Equal("unresolved", reference.Status);
        Assert.Equal("GRD-MISSING", reference.Ref);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "decision_refs.guard_unresolved");
    }

    [Fact]
    public void Inspect_MissingGuardDecisionFileWarnsWithoutBlocking()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket(decisionRefs: """
        [
          "guard-run:GRD-123"
        ]
        """));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("ready", result.Readiness.Decision);
        var reference = Assert.Single(result.DecisionRefs);
        Assert.Equal("unresolved", reference.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "decision_refs.guard_decisions_missing");
    }

    [Fact]
    public void Inspect_UnkindedPlainDecisionReferenceIsPreservedAsUnvalidatedExternalRef()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", "{\"schema_version\":1,\"run_id\":\"GRD-123\",\"outcome\":\"allow\"}\n");
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket(decisionRefs: """
        [
          "external-ticket-123"
        ]
        """));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("ready", result.Readiness.Decision);
        var reference = Assert.Single(result.DecisionRefs);
        Assert.False(reference.IsGuardCandidate);
        Assert.Equal("unvalidated", reference.Status);
        Assert.Equal("external-ticket-123", reference.Ref);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "decision_refs.guard_unresolved");
    }
}
