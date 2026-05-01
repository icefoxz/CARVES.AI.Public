using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeDefaultWorkflowProof()
    {
        return OperatorSurfaceFormatter.RuntimeDefaultWorkflowProof(BuildRuntimeDefaultWorkflowProof());
    }

    public OperatorCommandResult ApiRuntimeDefaultWorkflowProof()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildRuntimeDefaultWorkflowProof()));
    }

    private RuntimeDefaultWorkflowProofSurface BuildRuntimeDefaultWorkflowProof()
    {
        var threadStart = CreateRuntimeAgentThreadStartService().Build();
        var shortContext = BuildRuntimeAgentShortContext(null);
        var markdownBudget = BuildRuntimeMarkdownReadPathBudget(null);
        var governanceCoverage = BuildRuntimeGovernanceSurfaceCoverageAudit();
        var resourcePack = CreateRuntimeExternalConsumerResourcePackService().Build();
        return new RuntimeDefaultWorkflowProofService(repoRoot).Build(
            threadStart,
            shortContext,
            markdownBudget,
            governanceCoverage,
            resourcePack);
    }
}
