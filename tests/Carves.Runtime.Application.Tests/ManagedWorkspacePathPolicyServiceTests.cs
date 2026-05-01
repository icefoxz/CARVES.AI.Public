using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Tests;

public sealed class ManagedWorkspacePathPolicyServiceTests
{
    [Fact]
    public void Evaluate_WithActiveLease_ClassifiesScopeEscapeAndProtectedRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryManagedWorkspaceLeaseRepository();
        repository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-001",
                    TaskId = "T-CARD-697-006",
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "lease-001"),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = ["src/Scoped/File.cs"],
                },
            ],
        });
        var service = new ManagedWorkspacePathPolicyService(workspace.RootPath, repository);

        var assessment = service.Evaluate(
            "T-CARD-697-006",
            [
                "src/Scoped/File.cs",
                "docs/OutOfScope.md",
                ".ai/tasks/graph.json",
                ".git/config",
            ]);

        Assert.True(assessment.EnforcementActive);
        Assert.True(assessment.LeaseAware);
        Assert.Equal("deny", assessment.Status);
        Assert.Equal("lease-001", assessment.LeaseId);
        Assert.Equal(1, assessment.WorkspaceOpenCount);
        Assert.Equal(1, assessment.ScopeEscapeCount);
        Assert.Equal(1, assessment.HostOnlyCount);
        Assert.Equal(1, assessment.DenyCount);
        Assert.Contains(assessment.TouchedPaths, item => item.Path == "docs/OutOfScope.md" && item.PolicyClass == "scope_escape");
        Assert.Contains(assessment.TouchedPaths, item => item.Path == ".ai/tasks/graph.json" && item.PolicyClass == "host_only");
        Assert.Contains(assessment.TouchedPaths, item => item.Path == ".git/config" && item.PolicyClass == "deny");
    }

    [Fact]
    public void BuildDefaultRules_IncludesModeDScopeEscapeAsFailClosedPolicy()
    {
        var rules = ManagedWorkspacePathPolicyService.BuildDefaultRules();

        Assert.Contains(rules, rule => rule.PolicyClass == "workspace_open" && rule.EnforcementEffect == "allow_inside_active_lease_scope");
        Assert.Contains(rules, rule => rule.PolicyClass == "review_required" && rule.EnforcementEffect == "allow_preparation_but_require_host_review_before_ingress");
        Assert.Contains(rules, rule => rule.PolicyClass == "scope_escape" && rule.EnforcementEffect == "fail_closed_and_require_replan");
        Assert.Contains(rules, rule => rule.PolicyClass == "host_only" && rule.EnforcementEffect == "fail_closed_and_route_to_host_writeback");
        Assert.Contains(rules, rule => rule.PolicyClass == "deny" && rule.EnforcementEffect == "deny_without_review_or_writeback");
    }

    [Fact]
    public void Evaluate_WithoutLease_StillProtectsDeniedAndHostOnlyRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ManagedWorkspacePathPolicyService(workspace.RootPath, new InMemoryManagedWorkspaceLeaseRepository());

        var assessment = service.Evaluate(
            "T-CARD-697-LEGACY",
            [
                ".carves-platform/runtime-state/state.json",
                ".env",
            ]);

        Assert.True(assessment.EnforcementActive);
        Assert.False(assessment.LeaseAware);
        Assert.Equal("deny", assessment.Status);
        Assert.Equal(1, assessment.HostOnlyCount);
        Assert.Equal(1, assessment.DenyCount);
        Assert.Equal(0, assessment.ScopeEscapeCount);
    }
}
