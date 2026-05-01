namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    public SessionGatewayOperationRegistrySurface GetTypedOperationRegistry()
    {
        return typedExecutionCoreService.GetOperationRegistry();
    }

    public SessionGatewayExecutionTransactionDryRunSurface VerifyTypedTransactionDryRun(SessionGatewayTransactionVerificationRequest request)
    {
        return typedExecutionCoreService.VerifyTransactionDryRun(request);
    }

    public SessionGatewayExecutionTransactionDryRunSurface CompileTypedTransactionDryRun(SessionGatewayCapabilityLeaseSurface capabilityLease)
    {
        return typedExecutionCoreService.CompileCanonicalTransactionDryRun(capabilityLease);
    }

    private SessionGatewayOperationRegistrySurface BuildOperationRegistry()
    {
        return typedExecutionCoreService.GetOperationRegistry();
    }

    private SessionGatewayExecutionTransactionDryRunSurface BuildCanonicalTransactionDryRun(SessionGatewayCapabilityLeaseSurface capabilityLease)
    {
        return typedExecutionCoreService.CompileCanonicalTransactionDryRun(capabilityLease);
    }

    private SessionGatewayExecutionTransactionDryRunSurface BuildNotRequiredTransactionDryRun()
    {
        return typedExecutionCoreService.BuildNotRequiredTransactionDryRun();
    }
}
