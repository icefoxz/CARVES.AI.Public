using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentFailureRecoveryClosureServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedFailureClassesAndOperatorHandoffPosture()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md", "# stage6");
        workspace.WriteFile("docs/runtime/runtime-failures.md", "# failures");
        workspace.WriteFile("docs/runtime/worker-failure-classification-retry-policy.md", "# retry");
        workspace.WriteFile("docs/runtime/recovery-policy-engine.md", "# recovery");
        workspace.WriteFile("docs/runtime/runtime-consistency-check.md", "# consistency");
        workspace.WriteFile("docs/runtime/delegated-worker-lifecycle-reconciliation.md", "# lifecycle");
        workspace.WriteFile("docs/runtime/expired-delegated-run-recovery-classification.md", "# expired");

        var service = new RuntimeAgentFailureRecoveryClosureService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-agent-failure-recovery-closure", surface.SurfaceId);
        Assert.Equal("bounded_failure_recovery_closure_ready", surface.OverallPosture);
        Assert.Equal("runtime_control_kernel", surface.TruthOwner);
        Assert.Contains("transient_infra", surface.RetryableFailureKinds, StringComparer.Ordinal);
        Assert.Contains("rebuild_worktree", surface.RecoveryOutcomes, StringComparer.Ordinal);
        Assert.Contains(surface.ReadOnlyVisibilityCommands, item => item.Contains("inspect runtime-agent-failure-recovery-closure", StringComparison.Ordinal));
        Assert.Contains(surface.OperatorHandoffCommands, item => item.Contains("reconcile runtime", StringComparison.Ordinal));
        Assert.Contains(surface.BlockedBehaviors, item => item.Contains("silent retry loops", StringComparison.Ordinal));
        Assert.Contains(surface.FailureClasses, item => item.FailureClassId == "delegated_lifecycle_drift" && item.RetryPosture == "reconcile_before_retry");
        Assert.Contains(surface.FailureClasses, item => item.FailureClassId == "semantic_task_failure" && item.RuntimeOutcome == "review_or_pending_only_when_retry_is_explicit");
        Assert.Contains(surface.NonClaims, item => item.Contains("recovery planner", StringComparison.Ordinal));
    }
}
