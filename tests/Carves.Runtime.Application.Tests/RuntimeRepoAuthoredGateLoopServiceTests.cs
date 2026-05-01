using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeRepoAuthoredGateLoopServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndRejectsMarkdownTruthOwnershipAndPrOnlyWorldview()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeRepoAuthoredGateLoopService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformRepoAuthoredGateLoopFile));
        Assert.Equal("runtime-repo-authored-gate-loop", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/repo-authored-gate-loop.json", surface.PolicyPath);
        Assert.Equal(".carves-platform/policies/repo-authored-gate-loop.json", surface.Policy.DefinitionKernel.MachineTruthPath);
        Assert.Contains(surface.Policy.DefinitionKernel.ProjectionPaths, item => item == ".continue/checks/");
        Assert.Contains(surface.Policy.DefinitionKernel.ProjectionPaths, item => item == ".agents/checks/");
        Assert.Contains(surface.Policy.GateExecution.ResultStates, item => item == "pass");
        Assert.Contains(surface.Policy.GateExecution.ResultStates, item => item == "fail");
        Assert.Contains(surface.Policy.GateExecution.ResultStates, item => item == "pending");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "review_definition_kernel");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "independent_gate_execution");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "remediation_accept_reject_boundary");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "workflow_projection_qualification");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "markdown_prompt_files_remain_projection_only");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "workflow_projections_are_not_pr_only");
        Assert.Contains(surface.Policy.WorkflowProjections, item => item.ProjectionId == "pull_request_ci_projection");
        Assert.Contains(surface.Policy.WorkflowProjections, item => item.ProjectionId == "branch_review_projection");
        Assert.Contains(surface.Policy.WorkflowProjections, item => item.ProjectionId == "local_operator_projection");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "github_only_worldview" && item.Readiness == "rejected");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "markdown_prompt_truth_owner" && item.Readiness == "rejected");
    }
}
