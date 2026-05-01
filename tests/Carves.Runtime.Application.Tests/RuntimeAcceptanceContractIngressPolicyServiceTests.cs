using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAcceptanceContractIngressPolicyServiceTests
{
    [Fact]
    public void Build_ClassifiesPlanningMutationAsSynthesisAndExecutionAsFailClosed()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/acceptance-contract-driven-planning.md", "# acceptance contract");
        workspace.WriteFile("docs/contracts/acceptance-contract.schema.json", "{}");

        var service = new RuntimeAcceptanceContractIngressPolicyService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-acceptance-contract-ingress-policy", surface.SurfaceId);
        Assert.Equal("bounded_acceptance_contract_ingress_policy_ready", surface.OverallPosture);
        Assert.Equal("auto_minimum_contract", surface.PlanningTruthMutationPolicy);
        Assert.Equal("explicit_gap_required", surface.ExecutionDispatchPolicy);
        Assert.Equal("planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", surface.PolicySummary);
        Assert.Contains(surface.Ingresses, item => item.IngressId == "card_draft_ingress" && item.ContractPolicy == "auto_minimum_contract");
        Assert.Contains(surface.Ingresses, item => item.IngressId == "taskgraph_draft_approval_ingress" && item.Triggers.Contains("approve-taskgraph-draft", StringComparer.Ordinal));
        Assert.Contains(surface.Ingresses, item => item.IngressId == "planner_proposal_acceptance_ingress" && item.SourceAnchors.Contains("PlannerProposalAcceptanceService.Accept", StringComparer.Ordinal));
        Assert.Contains(surface.Ingresses, item => item.IngressId == "execution_dispatch_ingress" && item.ContractPolicy == "explicit_gap_required");
        Assert.Contains(surface.NonClaims, item => item.Contains("execution dispatch", StringComparison.Ordinal));
    }
}
