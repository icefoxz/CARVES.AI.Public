using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private RuntimeGovernanceProgramReauditService CreateRuntimeGovernanceProgramReauditService()
    {
        return new RuntimeGovernanceProgramReauditService(
            repoRoot,
            paths,
            systemConfig,
            runtimePolicyBundleService.LoadRoleGovernancePolicy(),
            artifactRepository,
            codeGraphQueryService,
            refactoringService,
            taskGraphService);
    }
}
