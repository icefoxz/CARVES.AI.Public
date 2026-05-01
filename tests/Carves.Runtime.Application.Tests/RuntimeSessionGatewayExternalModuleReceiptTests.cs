using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionGatewayExternalModuleReceiptTests
{
    [Fact]
    public void SubmitMessage_WithVerifiedExternalModuleReceipts_CitesReceiptHashesOnAdmittedDryRun()
    {
        using var workspace = new TemporaryWorkspace();
        var receiptService = new ExternalModuleReceiptService(workspace.Paths);
        var outputPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report.json", "{\"verdict\":\"allow\"}");
        var stored = receiptService.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-F3-OK",
            TransactionId = "TX-F3-OK",
            ModuleId = "guard",
            InputContractJson = "{}",
            OutputArtifactPath = outputPath,
            OutputArtifactHash = receiptService.HashFile(outputPath),
            Verdict = "allow",
        });
        var gateway = CreateGateway(workspace.RootPath);
        var session = gateway.CreateOrResumeSession(new SessionGatewaySessionCreateRequest
        {
            ActorIdentity = "tester",
        });

        var turn = gateway.SubmitMessage(session.SessionId, new SessionGatewayMessageRequest
        {
            RequestedMode = "governed_run",
            UserText = "execute task",
            TargetTaskId = "TASK-F3-001",
            ExternalModuleReceiptPaths = [stored.ReceiptPath!],
            RequiredExternalModuleIds = ["guard"],
        });

        Assert.Equal("admitted_dry_run", turn.WorkOrderDryRun.AdmissionState);
        Assert.Equal("verified", turn.WorkOrderDryRun.ExternalModuleReceiptEvaluation.EvaluationState);
        Assert.Equal("cite_receipts", turn.WorkOrderDryRun.ExternalModuleReceiptEvaluation.TransactionDisposition);
        Assert.Contains(stored.Receipt!.ReceiptHash, turn.WorkOrderDryRun.ExternalModuleReceiptEvaluation.CitedReceiptHashes);
    }

    [Fact]
    public void VerifyExternalModuleReceipts_WithUntrustedExternalModuleReceipts_DowngradesDecision()
    {
        using var workspace = new TemporaryWorkspace();
        var receiptService = new ExternalModuleReceiptService(workspace.Paths);
        var outputPath = workspace.WriteFile(".ai/runtime/modules/audit/audit-report.json", "{\"verdict\":\"review\"}");
        var stored = receiptService.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-F3-DOWNGRADE",
            TransactionId = "TX-F3-DOWNGRADE",
            ModuleId = "audit",
            InputContractJson = "{}",
            OutputArtifactPath = outputPath,
            OutputArtifactHash = receiptService.HashFile(outputPath),
            Verdict = "review",
            TrustLevel = ExternalModuleReceiptTrustLevel.Untrusted,
        });
        var gateway = CreateGateway(workspace.RootPath);
        var session = gateway.CreateOrResumeSession(new SessionGatewaySessionCreateRequest
        {
            ActorIdentity = "tester",
        });

        var evaluation = gateway.VerifyExternalModuleReceipts(new SessionGatewayMessageRequest
        {
            RequestedMode = "governed_run",
            UserText = "execute task",
            TargetTaskId = "TASK-F3-002",
            ExternalModuleReceiptPaths = [stored.ReceiptPath!],
            RequiredExternalModuleIds = ["audit"],
        });

        Assert.Equal("downgraded", evaluation.EvaluationState);
        Assert.Equal("downgrade_transaction", evaluation.TransactionDisposition);
        Assert.Contains(ExternalModuleReceiptService.ProviderUntrustedStopReason, evaluation.StopReasons);
    }

    [Fact]
    public void SubmitMessage_BlocksWhenRequiredExternalModuleReceiptIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var receiptService = new ExternalModuleReceiptService(workspace.Paths);
        var outputPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report.json", "{\"verdict\":\"allow\"}");
        var stored = receiptService.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-F3-BLOCK",
            TransactionId = "TX-F3-BLOCK",
            ModuleId = "guard",
            InputContractJson = "{}",
            OutputArtifactPath = outputPath,
            OutputArtifactHash = receiptService.HashFile(outputPath),
            Verdict = "allow",
        });
        var gateway = CreateGateway(workspace.RootPath);
        var session = gateway.CreateOrResumeSession(new SessionGatewaySessionCreateRequest
        {
            ActorIdentity = "tester",
        });

        var turn = gateway.SubmitMessage(session.SessionId, new SessionGatewayMessageRequest
        {
            RequestedMode = "governed_run",
            UserText = "execute task",
            TargetTaskId = "TASK-F3-003",
            ExternalModuleReceiptPaths = [stored.ReceiptPath!],
            RequiredExternalModuleIds = ["guard", "handoff"],
        });

        Assert.Equal("blocked", turn.WorkOrderDryRun.AdmissionState);
        Assert.Equal("blocked", turn.WorkOrderDryRun.ExternalModuleReceiptEvaluation.EvaluationState);
        Assert.Equal("stop_transaction", turn.WorkOrderDryRun.ExternalModuleReceiptEvaluation.TransactionDisposition);
        Assert.Equal("repair_external_module_receipts", turn.WorkOrderDryRun.NextRequiredAction);
        Assert.Contains(ExternalModuleReceiptService.ReceiptMissingStopReason, turn.WorkOrderDryRun.StopReasons);
    }

    private static RuntimeSessionGatewayService CreateGateway(string repoRoot)
    {
        var eventStreamService = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), eventStreamService);
        return new RuntimeSessionGatewayService(repoRoot, actorSessionService, eventStreamService, () => "runtime-session-f3");
    }
}
