using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeGovernanceSurfaceCoverageAuditServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedGovernanceSurfaceCoverage()
    {
        using var workspace = new TemporaryWorkspace();
        WriteCoverageEvidence(workspace);

        var surface = new RuntimeGovernanceSurfaceCoverageAuditService(workspace.RootPath)
            .Build(RuntimeSurfaceCommandRegistry.CommandNames, BuildResourcePack());

        Assert.Equal("runtime-governance-surface-coverage-audit", surface.SurfaceId);
        Assert.True(surface.CoverageComplete);
        Assert.True(surface.LifecycleBudgetComplete);
        Assert.Equal("governance_surface_coverage_ready", surface.OverallPosture);
        Assert.Equal(0, surface.BlockingGapCount);
        Assert.Equal(12, surface.RequiredSurfaceCount);
        Assert.Equal(12, surface.MaxGovernanceCriticalSurfaceCount);
        Assert.Equal(2, surface.DefaultPathSurfaceCount);
        Assert.Equal(2, surface.MaxDefaultPathSurfaceCount);
        Assert.Equal(3, surface.AuditHandoffSurfaceCount);
        Assert.Equal(4, surface.MaxAuditHandoffSurfaceCount);
        Assert.True(surface.RegisteredSurfaceCount >= surface.RequiredSurfaceCount);
        Assert.Contains(surface.CoverageDimensions, dimension => dimension == "runtime_surface_registry");
        Assert.Contains(surface.CoverageDimensions, dimension => dimension == "surface_lifecycle_class");
        Assert.Contains(surface.CoverageDimensions, dimension => dimension == "read_path_budget_class");
        Assert.Contains(surface.RequiredSurfaces, surfaceId => surfaceId == "runtime-worker-execution-audit");
        Assert.Contains(surface.Entries, entry =>
            entry.SurfaceId == "runtime-agent-short-context"
            && entry.CoverageStatus == "covered"
            && entry.LifecycleClass == "active_default_reorientation"
            && entry.ReadPathClass == "default_path"
            && entry.CountsTowardDefaultPathBudget
            && entry.ResourcePackCovered
            && entry.HostContractCovered);
        Assert.Contains(surface.Entries, entry =>
            entry.SurfaceId == "packet-enforcement"
            && entry.CoverageStatus == "covered"
            && entry.ReadPathClass == "internal_gate"
            && !entry.ResourcePackRequired);
        Assert.Contains(surface.Entries, entry =>
            entry.SurfaceId == "runtime-worker-execution-audit"
            && entry.ReadPathClass == "audit_handoff"
            && entry.DefaultPathParticipation == "troubleshooting_only");
        Assert.Empty(surface.LifecycleBudgetGaps);
        Assert.Contains(surface.NonClaims, claim =>
            claim.Contains("does not prove behavior correctness", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim =>
            claim.Contains("default read path", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_FlagsMissingRegistryCoverageAsBlockingGap()
    {
        using var workspace = new TemporaryWorkspace();
        WriteCoverageEvidence(workspace);
        var registered = RuntimeSurfaceCommandRegistry.CommandNames
            .Where(name => !string.Equals(name, "runtime-worker-execution-audit", StringComparison.Ordinal))
            .ToArray();

        var surface = new RuntimeGovernanceSurfaceCoverageAuditService(workspace.RootPath)
            .Build(registered, BuildResourcePack());

        Assert.False(surface.CoverageComplete);
        Assert.Equal("governance_surface_coverage_blocked_by_gaps", surface.OverallPosture);
        Assert.Contains(surface.Gaps, gap => gap == "registry_missing:runtime-worker-execution-audit");
        Assert.True(surface.LifecycleBudgetComplete);
        var entry = Assert.Single(surface.Entries, entry => entry.SurfaceId == "runtime-worker-execution-audit");
        Assert.Equal("blocking_gap", entry.CoverageStatus);
        Assert.Equal("active_audit_query", entry.LifecycleClass);
        Assert.Equal("audit_handoff", entry.ReadPathClass);
    }

    private static void WriteCoverageEvidence(TemporaryWorkspace workspace)
    {
        workspace.WriteFile(
            "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md",
            """
            carves agent start --json
            carves agent context --json
            carves api runtime-markdown-read-path-budget
            carves api runtime-worker-execution-audit status:Failed
            carves api runtime-default-workflow-proof
            runtime-default-workflow-proof
            bootstrap packet
            task overlay
            """);
        workspace.WriteFile(
            "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md",
            """
            runtime-agent-thread-start
            runtime-agent-short-context
            runtime-markdown-read-path-budget
            runtime-worker-execution-audit
            runtime-governed-agent-handoff-proof
            runtime-default-workflow-proof
            carves api runtime-default-workflow-proof
            bootstrap packet
            task overlay
            """);
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs",
            """
            runtime-agent-thread-start
            runtime-governed-agent-handoff-proof
            runtime-workspace-mutation-audit
            """);
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs",
            """
            runtime-agent-short-context
            runtime-markdown-read-path-budget
            runtime-agent-bootstrap-packet
            runtime-agent-task-overlay
            """);
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/RuntimeWorkerExecutionAuditHostContractTests.cs",
            "runtime-worker-execution-audit");
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/HostContractTests.cs",
            """
            execution-packet
            packet-enforcement
            """);
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/RuntimeBrokeredExecutionHostContractTests.cs",
            "runtime-brokered-execution");
        workspace.WriteFile(
            "tests/Carves.Runtime.IntegrationTests/RuntimeDefaultWorkflowProofHostContractTests.cs",
            "runtime-default-workflow-proof");
    }

    private static RuntimeExternalConsumerResourcePackSurface BuildResourcePack()
    {
        return new RuntimeExternalConsumerResourcePackSurface
        {
            CommandEntries =
            [
                BuildCommand("carves agent start --json", "runtime-agent-thread-start"),
                BuildCommand("carves agent context --json", "runtime-agent-short-context"),
                BuildCommand("carves api runtime-markdown-read-path-budget", "runtime-markdown-read-path-budget"),
                BuildCommand("carves api runtime-worker-execution-audit <query>", "runtime-worker-execution-audit"),
                BuildCommand("carves agent handoff --json", "runtime-governed-agent-handoff-proof"),
                BuildCommand("carves api runtime-default-workflow-proof", "runtime-default-workflow-proof"),
            ],
        };
    }

    private static RuntimeExternalConsumerCommandEntrySurface BuildCommand(string command, string surfaceId)
    {
        return new RuntimeExternalConsumerCommandEntrySurface
        {
            Command = command,
            SurfaceId = surfaceId,
            AuthorityClass = "read_only",
            ConsumerUse = "test coverage entry",
        };
    }
}
