using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class ExternalModuleReceiptServiceTests
{
    [Fact]
    public void BuildRegistry_ProjectsInitialModulesWithoutCopyingInternalGovernanceRules()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);

        var registry = service.BuildRegistry();

        Assert.Equal(ExternalModuleReceiptService.ReceiptRegistryVersion, registry.RegistryVersion);
        Assert.Contains(registry.Modules, module => module.ModuleId == "guard" && module.ModuleOwner == "CARVES.Guard");
        Assert.Contains(registry.Modules, module => module.ModuleId == "handoff" && module.CapabilityId == "adapter.handoff");
        Assert.Contains(registry.Modules, module => module.ModuleId == "matrix" && module.VerdictSchema == "carves.matrix.verdict.v0.98-rc");
        Assert.Contains(registry.Modules, module => module.ModuleId == "audit");
        Assert.Contains(registry.Modules, module => module.ModuleId == "shield");
        Assert.Contains(registry.Modules, module => module.ModuleId == "codegraph_projection");
        Assert.Contains(registry.Modules, module => module.ModuleId == "memory_proposalizer");
        Assert.All(registry.Modules, module =>
        {
            Assert.Equal("receipt_hash_and_output_artifact_hash_must_replay", module.ReceiptReplayRule);
            Assert.Equal("receipt_only_no_internal_rules", module.GovernanceBoundary);
        });
    }

    [Fact]
    public void StoreReceipt_VerifiesOutputHashAndBuildsDecisionCitation()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var outputPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report.json", "{\"verdict\":\"allow\"}");
        var outputHash = service.HashFile(outputPath);

        var stored = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-001",
            TransactionId = "TX-P8-001",
            OperationId = "verify_external_module_receipts",
            ModuleId = "guard",
            InputContractJson = "{\"patch_hash\":\"sha256:patch\"}",
            OutputArtifactPath = outputPath,
            OutputArtifactHash = outputHash,
            Verdict = "allow",
            TrustLevel = ExternalModuleReceiptTrustLevel.Trusted,
            Summary = "Guard receipt accepted.",
        });
        var replay = service.ReplayReceipt(stored.ReceiptPath!);
        var decision = service.EvaluateForDecision([replay]);

        Assert.True(stored.Verified);
        Assert.False(stored.ShouldStop);
        Assert.NotNull(stored.Receipt);
        Assert.Equal("guard", stored.Receipt!.ModuleId);
        Assert.Equal("CARVES.Guard", stored.Receipt.ModuleOwner);
        Assert.Equal("carves.guard.input_contract.v0.98-rc", stored.Receipt.InputContractSchema);
        Assert.Equal(outputHash, stored.Receipt.OutputArtifactHash);
        Assert.True(stored.Receipt.OutputArtifactVerified);
        Assert.False(string.IsNullOrWhiteSpace(stored.Receipt.InputContractHash));
        Assert.False(string.IsNullOrWhiteSpace(stored.Receipt.ReceiptHash));
        Assert.Equal(stored.Receipt.ReceiptHash, stored.Citation.ReceiptHash);
        Assert.Equal("cite_receipt", stored.TransactionDisposition);
        Assert.True(File.Exists(stored.ReceiptPath));

        Assert.Equal("verified", replay.ReplayState);
        Assert.True(replay.Verified);
        Assert.Equal(stored.Receipt.ReceiptHash, replay.Citation.ReceiptHash);
        Assert.Equal("cite_receipts", decision.TransactionDisposition);
        Assert.Contains(stored.Receipt.ReceiptHash, decision.CitedReceiptHashes);
        Assert.Empty(decision.StopReasons);
    }

    [Fact]
    public void CallAndStoreReceipt_StoresReceiptReturnedByExternalModuleAdapter()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var called = false;
        var stored = service.CallAndStoreReceipt(
            new ExternalModuleCallRequest
            {
                WorkOrderId = "WO-P8-CALL",
                TransactionId = "TX-P8-CALL",
                OperationId = "verify_external_module_receipts",
                ModuleId = "matrix",
                InputContractJson = "{\"work_order_id\":\"WO-P8-CALL\"}",
            },
            context =>
            {
                called = true;
                Assert.Equal("matrix", context.ModuleId);
                Assert.Equal("CARVES.Matrix", context.ModuleOwner);
                Assert.Equal("adapter.matrix", context.CapabilityId);
                Assert.False(string.IsNullOrWhiteSpace(context.InputContractHash));
                var outputPath = workspace.WriteFile(".ai/runtime/modules/matrix/matrix-decision.json", "{\"verdict\":\"allow\"}");
                return new ExternalModuleInvocationResult(
                    outputPath,
                    service.HashFile(outputPath),
                    "allow",
                    "carves.matrix.verdict.v0.98-rc",
                    ExternalModuleReceiptTrustLevel.Trusted,
                    "Matrix receipt accepted.",
                    new Dictionary<string, string?> { ["decision_kind"] = "allow" });
            });

        Assert.True(called);
        Assert.True(stored.Verified);
        Assert.Equal("matrix", stored.Receipt!.ModuleId);
        Assert.Equal("decision_kind", Assert.Single(stored.Receipt.Facts).Key);
        Assert.Equal("allow", stored.Citation.Verdict);
    }

    [Fact]
    public void StoreReceipt_UnverifiableOutputStopsAndUntrustedOutputDowngrades()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var unverifiedOutputPath = workspace.WriteFile(".ai/runtime/modules/shield/shield-report.json", "{\"verdict\":\"allow\"}");
        var untrustedOutputPath = workspace.WriteFile(".ai/runtime/modules/audit/audit-report.json", "{\"verdict\":\"review\"}");
        var untrustedHash = service.HashFile(untrustedOutputPath);

        var unverifiable = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-STOP",
            TransactionId = "TX-P8-STOP",
            ModuleId = "shield",
            InputContractJson = "{}",
            OutputArtifactPath = unverifiedOutputPath,
            OutputArtifactHash = "sha256:not-the-file",
            Verdict = "allow",
            TrustLevel = ExternalModuleReceiptTrustLevel.Trusted,
        });
        var untrusted = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-DOWNGRADE",
            TransactionId = "TX-P8-DOWNGRADE",
            ModuleId = "audit",
            InputContractJson = "{}",
            OutputArtifactPath = untrustedOutputPath,
            OutputArtifactHash = untrustedHash,
            Verdict = "review",
            TrustLevel = ExternalModuleReceiptTrustLevel.Untrusted,
        });
        var decision = service.EvaluateForDecision([
            service.ReplayReceipt(unverifiable.ReceiptPath!),
            service.ReplayReceipt(untrusted.ReceiptPath!),
        ]);

        Assert.True(unverifiable.ShouldStop);
        Assert.Equal("unverifiable", unverifiable.Receipt!.ReceiptState);
        Assert.Contains(ExternalModuleReceiptService.ToolOutputUnverifiedStopReason, unverifiable.StopReasons);
        Assert.Equal("stop_transaction", unverifiable.TransactionDisposition);

        Assert.True(untrusted.ShouldDowngrade);
        Assert.Equal("untrusted", untrusted.Receipt!.ReceiptState);
        Assert.Contains(ExternalModuleReceiptService.ProviderUntrustedStopReason, untrusted.StopReasons);
        Assert.Equal("downgrade_transaction", untrusted.TransactionDisposition);

        Assert.Equal("stop_transaction", decision.TransactionDisposition);
        Assert.Contains(ExternalModuleReceiptService.ToolOutputUnverifiedStopReason, decision.StopReasons);
        Assert.Contains(ExternalModuleReceiptService.ProviderUntrustedStopReason, decision.StopReasons);
        Assert.Contains(untrusted.Receipt.ReceiptHash, decision.CitedReceiptHashes);
    }

    [Fact]
    public void ReplayReceipt_DetectsTamperedOutputArtifact()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var outputPath = workspace.WriteFile(".ai/runtime/modules/handoff/handoff.json", "{\"verdict\":\"allow\"}");
        var outputHash = service.HashFile(outputPath);
        var stored = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-TAMPER",
            TransactionId = "TX-P8-TAMPER",
            ModuleId = "handoff",
            InputContractJson = "{}",
            OutputArtifactPath = outputPath,
            OutputArtifactHash = outputHash,
            Verdict = "allow",
        });
        File.WriteAllText(outputPath, "{\"verdict\":\"changed\"}");

        var replay = service.ReplayReceipt(stored.ReceiptPath!);

        Assert.Equal("unverifiable", replay.ReplayState);
        Assert.True(replay.ShouldStop);
        Assert.Contains(ExternalModuleReceiptService.ToolOutputUnverifiedStopReason, replay.StopReasons);
    }

    [Fact]
    public void VerifyReceipts_StopsWhenRequiredModuleReceiptIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var guardPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report.json", "{\"verdict\":\"allow\"}");
        var guardHash = service.HashFile(guardPath);
        var stored = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-MISSING",
            TransactionId = "TX-P8-MISSING",
            ModuleId = "guard",
            InputContractJson = "{}",
            OutputArtifactPath = guardPath,
            OutputArtifactHash = guardHash,
            Verdict = "allow",
        });

        var verification = service.VerifyReceipts(new ExternalModuleReceiptVerificationRequest
        {
            ReceiptPaths = [stored.ReceiptPath!],
            RequiredModuleIds = ["guard", "handoff"],
        });

        Assert.Equal("stop_transaction", verification.TransactionDisposition);
        Assert.Contains(ExternalModuleReceiptService.ReceiptMissingStopReason, verification.StopReasons);
        Assert.Contains(stored.Receipt!.ReceiptHash, verification.CitedReceiptHashes);
    }

    [Fact]
    public void VerifyReceipts_StopsOnDuplicateModuleReceipts()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExternalModuleReceiptService(workspace.Paths);
        var firstPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report-one.json", "{\"verdict\":\"allow\"}");
        var secondPath = workspace.WriteFile(".ai/runtime/modules/guard/guard-report-two.json", "{\"verdict\":\"allow\"}");
        var first = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-DUP",
            TransactionId = "TX-P8-DUP",
            ModuleId = "guard",
            InputContractJson = "{\"slot\":1}",
            OutputArtifactPath = firstPath,
            OutputArtifactHash = service.HashFile(firstPath),
            Verdict = "allow",
        });
        var second = service.StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = "WO-P8-DUP",
            TransactionId = "TX-P8-DUP",
            ModuleId = "guard",
            InputContractJson = "{\"slot\":2}",
            OutputArtifactPath = secondPath,
            OutputArtifactHash = service.HashFile(secondPath),
            Verdict = "allow",
        });

        var verification = service.VerifyReceipts(new ExternalModuleReceiptVerificationRequest
        {
            ReceiptPaths = [first.ReceiptPath!, second.ReceiptPath!],
        });

        Assert.Equal("stop_transaction", verification.TransactionDisposition);
        Assert.Contains(ExternalModuleReceiptService.DuplicateModuleStopReason, verification.StopReasons);
        Assert.Contains(first.Receipt!.ReceiptHash, verification.CitedReceiptHashes);
        Assert.Contains(second.Receipt!.ReceiptHash, verification.CitedReceiptHashes);
    }
}
