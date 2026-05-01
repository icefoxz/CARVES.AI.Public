using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSemanticCorrectnessDeathTestEvidenceServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedSemanticCorrectnessEvidenceTruth()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-semantic-correctness-evidence-workmap.md", "# workmap");
        workspace.WriteFile("docs/runtime/runtime-semantic-correctness-gap-and-death-test-doctrine.md", "# doctrine");
        workspace.WriteFile("docs/runtime/runtime-central-interaction-point-and-official-truth-ingress.md", "# ingress");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# reaudit");
        workspace.WriteFile("docs/runtime/runtime-direct-agent-vs-governed-comparison-packet.md", "# comparison");
        workspace.WriteFile("docs/runtime/runtime-semantic-miss-taxonomy.md", "# taxonomy");
        workspace.WriteFile("docs/runtime/runtime-control-plane-cost-ledger.md", "# ledger");
        workspace.WriteFile("docs/session-gateway/capability-forge-governance-ai-positioning.md", "# positioning");
        workspace.WriteFile("docs/session-gateway/capability-forge-retirement-routing.md", "# retirement");

        var service = new RuntimeSemanticCorrectnessDeathTestEvidenceService(workspace.RootPath);
        var surface = service.Build();

        Assert.Equal("runtime-semantic-correctness-death-test-evidence", surface.SurfaceId);
        Assert.Equal("semantic_correctness_death_test_evidence_ready", surface.OverallPosture);
        Assert.Equal("609_line_semantic_correctness_death_test_evidence", surface.CurrentLine);
        Assert.Equal("610_line_bootstrap_and_onboarding", surface.DeferredNextLine);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal(3, surface.DeathTestCount);
        Assert.True(surface.SemanticMissCategoryCount >= 5);
        Assert.True(surface.ControlPlaneCostBucketCount >= 5);
        Assert.Equal(3, surface.EvidencePackets.Count);
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
        Assert.Contains(surface.NonClaims, item => item.Contains("does not prove", StringComparison.OrdinalIgnoreCase));
    }
}
