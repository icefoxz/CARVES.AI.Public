using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Evidence;

namespace Carves.Runtime.IntegrationTests;

public sealed class TokenBudgetToolSurfaceTests
{
    [Fact]
    public void ContextEstimate_ColdCommand_BuildsTelemetryBackedContextPacket()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-M5-CONTEXT-001", scope: ["README.md"]);

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "context", "estimate", "T-M5-CONTEXT-001");
        var telemetry = new ContextBudgetTelemetryService(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).ListRecent();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Context estimate for T-M5-CONTEXT-001:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Budget posture:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Telemetry: CTXTEL-", result.StandardOutput, StringComparison.Ordinal);
        Assert.NotEmpty(telemetry);
        Assert.Contains(telemetry, item => string.Equals(item.TaskId, "T-M5-CONTEXT-001", StringComparison.Ordinal));
    }

    [Fact]
    public void EvidenceSearch_ColdCommand_FindsTaskEvidenceAndWritesTelemetry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-M5-EVIDENCE-001", scope: ["README.md"]);

        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "context", "estimate", "T-M5-EVIDENCE-001").ExitCode);

        var result = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "evidence",
            "search",
            "--task-id",
            "T-M5-EVIDENCE-001",
            "--kind",
            "context");
        var telemetry = new ContextBudgetTelemetryService(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).ListRecent();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Evidence search:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CTXEVI-", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Telemetry: CTXTEL-", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(telemetry, item => string.Equals(item.OperationKind, "evidence_search", StringComparison.Ordinal));
    }

    [Fact]
    public void MemoryPromote_ColdCommand_PromotesEvidenceToCanonicalAndSearchesIt()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-M5-MEMORY-001", scope: ["README.md"]);

        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "context", "estimate", "T-M5-MEMORY-001").ExitCode);

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var evidenceStore = new RuntimeEvidenceStoreService(paths);
        var evidence = evidenceStore.TryGetLatest("T-M5-MEMORY-001", RuntimeEvidenceKind.ContextPack);
        Assert.NotNull(evidence);

        var promote = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "memory",
            "promote",
            "--from-evidence",
            evidence!.EvidenceId,
            "--canonical",
            "--category",
            "project",
            "--actor",
            "integration-test");
        var search = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "memory",
            "search",
            "T-M5-MEMORY-001",
            "--scope",
            "task:T-M5-MEMORY-001");

        Assert.Equal(0, promote.ExitCode);
        Assert.Contains("Canonical fact: MEMFACT-", promote.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Evidence source: CTXEVI-", promote.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, search.ExitCode);
        Assert.Contains("Memory search: T-M5-MEMORY-001", search.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- fact:", search.StandardOutput, StringComparison.Ordinal);
        Assert.True(Directory.EnumerateFiles(paths.EvidenceFactsRoot, "*.json", SearchOption.TopDirectoryOnly).Any());
        Assert.True(Directory.EnumerateFiles(paths.MemoryPromotionsRoot, "*.json", SearchOption.TopDirectoryOnly).Any());
    }
}
