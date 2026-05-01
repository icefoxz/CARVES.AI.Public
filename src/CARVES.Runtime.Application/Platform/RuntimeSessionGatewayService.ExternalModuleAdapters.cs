using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    public SessionGatewayExternalModuleAdapterRegistrySurface GetExternalModuleAdapterRegistry()
    {
        return BuildExternalModuleAdapterRegistry();
    }

    public SessionGatewayExternalModuleReceiptEvaluationSurface VerifyExternalModuleReceipts(
        SessionGatewayMessageRequest request)
    {
        return BuildExternalModuleReceiptEvaluation(request);
    }

    private SessionGatewayExternalModuleAdapterRegistrySurface BuildExternalModuleAdapterRegistry()
    {
        var registry = externalModuleReceiptService.BuildRegistry();
        return new SessionGatewayExternalModuleAdapterRegistrySurface
        {
            RegistryVersion = registry.RegistryVersion,
            Modules = registry.Modules.Select(ProjectExternalModuleAdapter).ToArray(),
        };
    }

    private static SessionGatewayExternalModuleAdapterSurface ProjectExternalModuleAdapter(
        ExternalModuleAdapterDefinition definition)
    {
        return new SessionGatewayExternalModuleAdapterSurface
        {
            ModuleId = definition.ModuleId,
            ModuleOwner = definition.ModuleOwner,
            CapabilityId = definition.CapabilityId,
            InputContractSchema = definition.InputContractSchema,
            OutputArtifactKind = definition.OutputArtifactKind,
            VerdictSchema = definition.VerdictSchema,
            DefaultTrustLevel = definition.DefaultTrustLevel.ToString().ToLowerInvariant(),
            ReceiptReplayRule = definition.ReceiptReplayRule,
            GovernanceBoundary = definition.GovernanceBoundary,
        };
    }

    private SessionGatewayExternalModuleReceiptEvaluationSurface BuildExternalModuleReceiptEvaluation(
        SessionGatewayMessageRequest request)
    {
        var verification = externalModuleReceiptService.VerifyReceipts(new ExternalModuleReceiptVerificationRequest
        {
            ReceiptPaths = request.ExternalModuleReceiptPaths,
            RequiredModuleIds = request.RequiredExternalModuleIds,
        });
        if (!verification.VerificationRequired)
        {
            return BuildNotRequiredExternalModuleReceiptEvaluation();
        }

        return new SessionGatewayExternalModuleReceiptEvaluationSurface
        {
            EvaluationState = verification.TransactionDisposition switch
            {
                "cite_receipts" => "verified",
                "downgrade_transaction" => "downgraded",
                _ => "blocked",
            },
            TransactionDisposition = verification.TransactionDisposition,
            CitedReceiptHashes = verification.CitedReceiptHashes,
            StopReasons = verification.StopReasons,
            Receipts = verification.Replays.Select(ProjectReceiptReplay).ToArray(),
            Summary = verification.Summary,
        };
    }

    private static SessionGatewayExternalModuleReceiptEvaluationSurface BuildNotRequiredExternalModuleReceiptEvaluation()
    {
        return new SessionGatewayExternalModuleReceiptEvaluationSurface
        {
            EvaluationState = "not_required",
            TransactionDisposition = "not_required",
            Summary = "External module receipt verification is not required for this request.",
        };
    }

    private static SessionGatewayExternalModuleReceiptReplaySurface ProjectReceiptReplay(
        ExternalModuleReceiptReplayResult replay)
    {
        return new SessionGatewayExternalModuleReceiptReplaySurface
        {
            ModuleId = replay.Receipt?.ModuleId ?? string.Empty,
            ModuleOwner = replay.Receipt?.ModuleOwner ?? string.Empty,
            Verdict = replay.Receipt?.Verdict ?? replay.Citation.Verdict,
            ReplayState = replay.ReplayState,
            TransactionDisposition = replay.TransactionDisposition,
            ReceiptRelativePath = replay.ReceiptRelativePath,
            ReceiptHash = replay.Citation.ReceiptHash,
            OutputArtifactHash = replay.Citation.OutputArtifactHash,
            StopReasons = replay.StopReasons,
            Summary = replay.Summary,
        };
    }
}
