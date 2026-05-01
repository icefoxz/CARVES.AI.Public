using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSemanticCorrectnessDeathTestEvidence()
    {
        return OperatorSurfaceFormatter.RuntimeSemanticCorrectnessDeathTestEvidence(
            CreateRuntimeSemanticCorrectnessDeathTestEvidenceService().Build());
    }

    public OperatorCommandResult ApiRuntimeSemanticCorrectnessDeathTestEvidence()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSemanticCorrectnessDeathTestEvidenceService().Build()));
    }

    private RuntimeSemanticCorrectnessDeathTestEvidenceService CreateRuntimeSemanticCorrectnessDeathTestEvidenceService()
    {
        return new RuntimeSemanticCorrectnessDeathTestEvidenceService(repoRoot);
    }
}
