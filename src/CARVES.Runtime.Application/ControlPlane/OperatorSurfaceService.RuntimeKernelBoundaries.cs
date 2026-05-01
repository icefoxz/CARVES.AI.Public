using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeContextKernel()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeContextKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeContextKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeContextKernelService().Build()));
    }

    public OperatorCommandResult InspectRuntimeKnowledgeKernel()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeKnowledgeKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeKnowledgeKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeKnowledgeKernelService().Build()));
    }

    public OperatorCommandResult InspectRuntimeDomainGraphKernel()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeDomainGraphKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeDomainGraphKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeDomainGraphKernelService().Build()));
    }

    public OperatorCommandResult InspectRuntimeExecutionKernel()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeExecutionKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeExecutionKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeExecutionKernelService().Build()));
    }

    public OperatorCommandResult InspectRuntimeArtifactPolicyKernel()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeArtifactPolicyKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeArtifactPolicyKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeArtifactPolicyKernelService().Build()));
    }

    public OperatorCommandResult InspectRuntimeKernelUpgradeQualification()
    {
        return OperatorSurfaceFormatter.RuntimeKernelBoundary(CreateRuntimeKernelUpgradeQualificationService().Build());
    }

    public OperatorCommandResult ApiRuntimeKernelUpgradeQualification()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeKernelUpgradeQualificationService().Build()));
    }

    private RuntimeContextKernelService CreateRuntimeContextKernelService()
    {
        return new RuntimeContextKernelService(repoRoot, paths);
    }

    private RuntimeKnowledgeKernelService CreateRuntimeKnowledgeKernelService()
    {
        return new RuntimeKnowledgeKernelService(repoRoot, paths);
    }

    private RuntimeDomainGraphKernelService CreateRuntimeDomainGraphKernelService()
    {
        return new RuntimeDomainGraphKernelService(repoRoot);
    }

    private RuntimeExecutionKernelService CreateRuntimeExecutionKernelService()
    {
        return new RuntimeExecutionKernelService(repoRoot, paths, systemConfig);
    }

    private RuntimeArtifactPolicyKernelService CreateRuntimeArtifactPolicyKernelService()
    {
        return new RuntimeArtifactPolicyKernelService(repoRoot, paths, systemConfig);
    }

    private static RuntimeKernelUpgradeQualificationService CreateRuntimeKernelUpgradeQualificationService()
    {
        return new RuntimeKernelUpgradeQualificationService();
    }
}
