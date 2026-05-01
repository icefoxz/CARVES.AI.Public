using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeKernelBoundaryServiceTests
{
    [Fact]
    public void ContextKernel_Build_ProjectsBoundedReadTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeContextKernelService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.Equal("runtime-context-kernel", surface.SurfaceId);
        Assert.Equal("context", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "context_pack_projection");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "windowed_read_projection");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "bounded_read_context");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "context_narrowing_preflight_first");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "execution_packet_preflight");
    }

    [Fact]
    public void KnowledgeKernel_Build_ProjectsDurableAndPromotionRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeKnowledgeKernelService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.Equal("runtime-knowledge-kernel", surface.SurfaceId);
        Assert.Equal("knowledge", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "knowledge_inbox" && item.PathRefs.Contains(".ai/memory/inbox/", StringComparer.Ordinal));
        Assert.Contains(surface.TruthRoots, item => item.RootId == "memory_promotion");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "temporal_fact_truth" && item.PathRefs.Contains(".ai/evidence/facts/", StringComparer.Ordinal));
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "promotion_requires_audit");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "temporal_facts_preserve_validity_windows");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "code_facts_live_in_codegraph");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "temporal_fact_ledger");
    }

    [Fact]
    public void DomainGraphKernel_Build_ProjectsCodegraphFirstRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeDomainGraphKernelService(workspace.RootPath);

        var surface = service.Build();

        Assert.Equal("runtime-domain-graph-kernel", surface.SurfaceId);
        Assert.Equal("domain_graph", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "codegraph_structure_roots" && item.PathRefs.Contains(".ai/codegraph/symbols/", StringComparer.Ordinal));
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "structure_facts_are_codegraph_first");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "scope_analysis" && string.Equals(item.EntryPoint, "ICodeGraphQueryService.AnalyzeScope", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecutionKernel_Build_ProjectsActorAndWorkspaceTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeExecutionKernelService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());

        var surface = service.Build();

        Assert.Equal("runtime-execution-kernel", surface.SurfaceId);
        Assert.Equal("execution", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "execution_task_truth");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "workspace_lifecycle_truth");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "one_execution_truth_spine");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "workspace_runtime_lifecycle");
    }

    [Fact]
    public void ArtifactPolicyKernel_Build_ProjectsOneEvidencePath()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeArtifactPolicyKernelService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());

        var surface = service.Build();

        Assert.Equal("runtime-artifact-policy-kernel", surface.SurfaceId);
        Assert.Equal("artifact_policy", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "artifact_bundle_truth");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "policy_bundle_truth");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "policy_bundle_governs_gate_decisions");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "policy_bundle_surface");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "runtime_export_profiles_surface");
    }

    [Fact]
    public void KernelUpgradeQualification_Build_ProjectsProofPathsAndGoNoGo()
    {
        var service = new RuntimeKernelUpgradeQualificationService();

        var surface = service.Build();

        Assert.Equal("runtime-kernel-upgrade-qualification", surface.SurfaceId);
        Assert.Equal("qualification", surface.KernelId);
        Assert.Contains(surface.TruthRoots, item => item.RootId == "execution_kernel_proof_path");
        Assert.Contains(surface.TruthRoots, item => item.RootId == "structure_freeze_proof_path");
        Assert.Contains(surface.BoundaryRules, item => item.RuleId == "no_go_if_parallel_truth_reappears");
        Assert.Contains(surface.GovernedReadPaths, item => item.PathId == "upgrade_qualification_surface");
    }
}
