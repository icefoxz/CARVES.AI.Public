using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeGitNativeCodingLoopServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndRejectsHostReplacementAndRepoMapTruthPromotion()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeGitNativeCodingLoopService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformGitNativeCodingLoopFile));
        Assert.Equal("runtime-git-native-coding-loop", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/git-native-coding-loop.json", surface.PolicyPath);
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "repo_map_projection");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "patch_first_interaction");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "git_native_commit_evidence_loop");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "lint_test_evidence_loop");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "qualification_without_host_replacement");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "repo_map_remains_projection_not_codegraph_truth");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "git_commit_loop_does_not_replace_host_writeback");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "host_governance_replacement_is_rejected");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "repo_map_as_codegraph_truth" && item.Readiness == "rejected");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "git_commit_as_truth_writeback" && item.Readiness == "rejected");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "host_governance_replacement" && item.Readiness == "rejected");
    }
}
