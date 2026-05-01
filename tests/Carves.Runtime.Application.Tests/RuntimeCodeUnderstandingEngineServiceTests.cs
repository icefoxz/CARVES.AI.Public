using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeCodeUnderstandingEngineServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndPreservesCodegraphFirstBoundaries()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeCodeUnderstandingEngineService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformCodeUnderstandingEngineFile));
        Assert.Equal("runtime-code-understanding-engine", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/code-understanding-engine.json", surface.PolicyPath);
        Assert.Equal(".ai/codegraph/", surface.Policy.StrengthensTruthRoot);
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "syntax_substrate" && item.SourceProjects.Contains("tree-sitter"));
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "structured_query_and_rewrite" && item.SourceProjects.Contains("ast-grep"));
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "semantic_index_protocol" && item.CurrentStatus == "target_corrected_but_unattached");
        Assert.Contains(surface.Policy.PrecisionTiers, item => item.TierId == "search_grade");
        Assert.Contains(surface.Policy.PrecisionTiers, item => item.TierId == "impact_grade");
        Assert.Contains(surface.Policy.PrecisionTiers, item => item.TierId == "governance_grade");
        Assert.Contains(surface.Policy.SemanticPathPilots, item => item.PilotId == "bounded_csharp_semantic_path" && item.TargetPrecisionTiers.Contains("impact_grade"));
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "local_scip_master_is_not_semantic_index_protocol");
        Assert.Contains(
            surface.Policy.ExtractionBoundary.RejectedAnchors,
            item => item.Contains("D:/Projects/CARVES/scip-master", StringComparison.Ordinal));
        Assert.Contains(surface.Policy.GovernedReadPaths, item => item.PathId == "codegraph_scope_analysis");
        Assert.Contains(surface.Policy.GovernedReadPaths, item => item.PathId == "bounded_csharp_semantic_path_pilot");
        Assert.Contains(surface.Policy.Qualification.SuccessCriteria, item => item.Contains("codegraph", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.Policy.Qualification.SuccessCriteria, item => item.Contains("search_grade", StringComparison.Ordinal));
        Assert.Contains(surface.Policy.Qualification.StopConditions, item => item.Contains("D:/Projects/CARVES/scip-master", StringComparison.Ordinal));
    }
}
