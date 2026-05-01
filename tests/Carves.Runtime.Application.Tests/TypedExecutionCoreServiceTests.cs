using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Tests;

public sealed class TypedExecutionCoreServiceTests
{
    [Fact]
    public void CompileCanonicalTransactionDryRun_ProducesDeterministicVerifiedTransaction()
    {
        var service = new TypedExecutionCoreService();
        var registry = service.GetOperationRegistry();
        var lease = new SessionGatewayCapabilityLeaseSurface
        {
            CapabilityIds = registry.Operations
                .Select(operation => operation.CapabilityRequired)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };

        var first = service.CompileCanonicalTransactionDryRun(lease);
        var second = service.CompileCanonicalTransactionDryRun(lease);

        Assert.Equal("session-gateway-operation-registry@0.98-rc.p1", registry.RegistryVersion);
        Assert.Equal("session-gateway-transaction-compiler@0.98-rc.p3", first.CompilerVersion);
        Assert.Equal("verified", first.VerificationState);
        Assert.Equal(first.TransactionHash, second.TransactionHash);
        Assert.Equal(first.TransactionId, second.TransactionId);
        Assert.Equal(registry.Operations.Count, first.VerificationReport.StepCount);
        Assert.Equal(0, first.VerificationReport.ErrorCount);
    }

    [Fact]
    public void VerifyTransactionDryRun_FailsUnknownOperationsAndUnregisteredWritesThroughSharedVerifier()
    {
        var service = new TypedExecutionCoreService();
        var registry = service.GetOperationRegistry();
        var inspect = Assert.Single(registry.Operations, item => item.OperationId == "inspect_bound_objects");

        var result = service.VerifyTransactionDryRun(new SessionGatewayTransactionVerificationRequest
        {
            CapabilityIds = [inspect.CapabilityRequired],
            Steps =
            [
                new SessionGatewayTransactionStepSurface
                {
                    StepId = "step-shared-verifier",
                    OperationId = "unknown_free_text_operation",
                    OperationVersion = "v0.98-rc",
                    CapabilityRequired = inspect.CapabilityRequired,
                    DeclaredEffects = inspect.DeclaredEffects,
                    WritesDeclared = ["ledger:bound_object_projection", "free_text_write_target"],
                    FailurePolicy = inspect.FailurePolicy,
                    LedgerEventSchema = inspect.LedgerEventSchema,
                },
            ],
        });

        Assert.Equal("failed", result.VerificationState);
        Assert.Contains(result.VerificationErrors, item => item.StartsWith("SC-UNKNOWN-OPERATION:", StringComparison.Ordinal));
        Assert.False(result.VerificationReport.OperationCoverage);
    }

    [Fact]
    public void RuntimeSessionGatewayService_DelegatesTypedExecutionCore()
    {
        using var workspace = new TemporaryWorkspace();
        var eventStreamService = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), eventStreamService);
        var gateway = new RuntimeSessionGatewayService(workspace.RootPath, actorSessionService, eventStreamService, () => "runtime-session-f1");
        var typedCore = new TypedExecutionCoreService();

        var directRegistry = typedCore.GetOperationRegistry();
        var gatewayRegistry = gateway.GetTypedOperationRegistry();
        var lease = new SessionGatewayCapabilityLeaseSurface
        {
            CapabilityIds = directRegistry.Operations
                .Select(operation => operation.CapabilityRequired)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };

        var direct = typedCore.CompileCanonicalTransactionDryRun(lease);
        var viaGateway = gateway.CompileTypedTransactionDryRun(lease);

        Assert.Equal(directRegistry.RegistryVersion, gatewayRegistry.RegistryVersion);
        Assert.Equal(directRegistry.Operations.Select(static item => item.OperationId), gatewayRegistry.Operations.Select(static item => item.OperationId));
        Assert.Equal(direct.TransactionHash, viaGateway.TransactionHash);
        Assert.Equal(direct.TransactionId, viaGateway.TransactionId);
        Assert.Equal(direct.VerificationState, viaGateway.VerificationState);
    }
}
