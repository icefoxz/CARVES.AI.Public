using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionContractSurfaceServiceTests
{
    [Fact]
    public void Build_ProjectsExecutionContractsWithLivePacketCompilerLineage()
    {
        var service = new ExecutionContractSurfaceService();

        var snapshot = service.Build();

        Assert.Equal("execution-contract-surface", snapshot.SurfaceId);
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "execution_packet"
            && contract.Availability == ExecutionContractAvailability.Defined
            && contract.SchemaPath == "docs/contracts/execution-packet.schema.json"
            && contract.CurrentTruthLineage.Contains("ExecutionPacketCompilerService"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "task_result_envelope"
            && contract.CurrentTruthLineage.Contains("ResultEnvelope"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "planner_verdict"
            && contract.CurrentTruthLineage.Contains("BoundaryDecision"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "pack_artifact"
            && contract.SchemaPath == "docs/contracts/pack-artifact.schema.json"
            && contract.CurrentTruthLineage.Contains("runtime admit-pack"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "runtime_pack_v1_manifest"
            && contract.SchemaPath == "docs/contracts/runtime-pack-v1.schema.json"
            && contract.CurrentTruthLineage.Contains("validate runtime-pack-v1")
            && contract.CurrentTruthLineage.Contains("runtime admit-pack-v1"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "runtime_pack_policy_package"
            && contract.SchemaPath == "docs/contracts/runtime-pack-policy-package.schema.json"
            && contract.CurrentTruthLineage.Contains("runtime preview-pack-policy"));
        Assert.Contains(snapshot.Contracts, contract =>
            contract.ContractId == "runtime_pack_attribution"
            && contract.SchemaPath == "docs/contracts/runtime-pack-attribution.schema.json"
            && contract.CurrentTruthLineage.Contains("inspect runtime-pack-admission"));
    }

    [Fact]
    public void Build_ProjectsExplicitQuarantineAndHumanReviewVerdicts()
    {
        var service = new ExecutionContractSurfaceService();

        var snapshot = service.Build();

        Assert.Contains(snapshot.PlannerVerdicts, verdict =>
            verdict.ContractId == "human_review"
            && verdict.LegacyVerdict == PlannerVerdict.HumanDecisionRequired.ToString()
            && verdict.RequiresHumanReview);
        Assert.Contains(snapshot.PlannerVerdicts, verdict =>
            verdict.ContractId == "quarantined"
            && verdict.LegacyVerdict is null
            && verdict.IndicatesQuarantine);
        Assert.Contains(snapshot.PlannerVerdicts, verdict =>
            verdict.ContractId == "replan"
            && verdict.LegacyVerdict == PlannerVerdict.SplitTask.ToString()
            && verdict.RequestsReplan);
    }
}
