using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class EffectLedgerServiceTests
{
    [Fact]
    public void ReplayWorkOrder_VerifiesHashChainSealAndBoundRuntimeFacts()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var workOrderId = "WO-P5-001";
        var ledgerPath = service.GetWorkOrderLedgerPath(workOrderId);
        var outputPath = workspace.WriteFile(".ai/runtime/work-orders/WO-P5-001/admission.json", "{\"admitted\":true}");
        var outputHash = service.HashFile(outputPath);

        var first = service.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-WO-P5-001",
                "work_order_admission",
                "host_admission",
                ["admit_work_order_dry_run"],
                ["admit_work_order_dry_run"],
                [service.BuildOutput("admission_result", outputPath, outputHash)],
                "admitted_dry_run")
            {
                WorkOrderId = workOrderId,
                LeaseId = "CL-P5-001",
                TransactionId = "TX-P5-001",
                UtteranceHash = "sha256:utterance",
                ObjectBindingHash = "sha256:objects",
                AdmissionState = "admitted_dry_run",
                TransactionHash = "sha256:transaction",
                TerminalState = "submitted_to_review",
                TransactionStepIds = ["step-01", "step-02"],
            });
        var seal = service.Seal(
            ledgerPath,
            new EffectLedgerSealDraft("EV-WO-P5-001", "host_admission")
            {
                WorkOrderId = workOrderId,
                LeaseId = "CL-P5-001",
                TransactionId = "TX-P5-001",
                UtteranceHash = "sha256:utterance",
                ObjectBindingHash = "sha256:objects",
                AdmissionState = "admitted_dry_run",
                TransactionHash = "sha256:transaction",
                TerminalState = "submitted_to_review",
                TransactionStepIds = ["step-01", "step-02"],
            });

        var replay = service.ReplayWorkOrder(workOrderId);

        Assert.Equal("verified", replay.ReplayState);
        Assert.True(replay.CanWriteBack);
        Assert.True(replay.Sealed);
        Assert.Equal(2, replay.EventCount);
        Assert.Equal(workOrderId, replay.WorkOrderId);
        Assert.Equal("sha256:utterance", replay.UtteranceHash);
        Assert.Equal("sha256:objects", replay.ObjectBindingHash);
        Assert.Equal("admitted_dry_run", replay.AdmissionState);
        Assert.Equal("CL-P5-001", replay.LeaseId);
        Assert.Equal("sha256:transaction", replay.TransactionHash);
        Assert.Equal("submitted_to_review", replay.TerminalState);
        Assert.Contains("work_order_admission", replay.StepEvents);
        Assert.Contains("step-01", replay.StepEvents);
        Assert.Equal(outputHash, replay.OutputHashes["admission_result"]);
        Assert.Equal(seal.EventHash, replay.LastEventHash);
        Assert.Contains(first.EventId, replay.EventIds);
        Assert.Empty(replay.StopReasons);
    }

    [Fact]
    public void Replay_DetectsHashChainBreakAsAuditIncomplete()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var ledgerPath = service.GetWorkOrderLedgerPath("WO-P5-BROKEN");

        var first = service.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-WO-P5-BROKEN",
                "first_step",
                "host_admission",
                ["first_effect"],
                ["first_effect"],
                [],
                "ok")
            {
                WorkOrderId = "WO-P5-BROKEN",
            });
        _ = service.Seal(
            ledgerPath,
            new EffectLedgerSealDraft("EV-WO-P5-BROKEN", "host_admission")
            {
                WorkOrderId = "WO-P5-BROKEN",
            });
        var lines = File.ReadAllLines(ledgerPath);
        lines[1] = lines[1].Replace(first.EventHash, "broken-hash", StringComparison.Ordinal);
        File.WriteAllLines(ledgerPath, lines);

        var replay = service.Replay(ledgerPath);

        Assert.Equal("broken", replay.ReplayState);
        Assert.False(replay.CanWriteBack);
        Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, replay.StopReasons);
        Assert.Contains("hash chain broke", replay.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Replay_RequiresFinalSealBeforeWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var ledgerPath = service.GetWorkOrderLedgerPath("WO-P5-UNSEALED");
        _ = service.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-WO-P5-UNSEALED",
                "work_order_admission",
                "host_admission",
                ["admit_work_order_dry_run"],
                ["admit_work_order_dry_run"],
                [],
                "admitted_dry_run")
            {
                WorkOrderId = "WO-P5-UNSEALED",
            });

        var replay = service.Replay(ledgerPath);

        Assert.Equal("broken", replay.ReplayState);
        Assert.False(replay.CanWriteBack);
        Assert.False(replay.Sealed);
        Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, replay.StopReasons);
        Assert.Equal("Effect ledger is missing its final seal event.", replay.Summary);
    }

    [Fact]
    public void AppendEvent_RejectsOutputsOutsideGovernedRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"carves-ledger-outside-{Guid.NewGuid():N}.json");
        File.WriteAllText(outsidePath, "{}");
        try
        {
            var ledgerPath = service.GetWorkOrderLedgerPath("WO-P5-PATH-GUARD");
            var error = Assert.Throws<InvalidOperationException>(() => service.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    "EV-WO-P5-PATH-GUARD",
                    "unsafe_output",
                    "host_admission",
                    ["bind_external_output"],
                    ["bind_external_output"],
                    [new EffectLedgerOutput("external", outsidePath, "sha256:placeholder")],
                    "blocked")));

            Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, error.Message, StringComparison.Ordinal);
            Assert.Contains("escapes the repository root", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Fact]
    public void AppendEvent_RejectsOutputsInsideRepoButOutsideGovernedArtifactRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var unguardedPath = workspace.WriteFile(".ai/unguarded/output.json", "{}");

        var ledgerPath = service.GetWorkOrderLedgerPath("WO-P7-ROOT-GUARD");
        var error = Assert.Throws<InvalidOperationException>(() => service.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-WO-P7-ROOT-GUARD",
                "unsafe_output",
                "host_admission",
                ["bind_unguarded_output"],
                ["bind_unguarded_output"],
                [new EffectLedgerOutput("unguarded", ".ai/unguarded/output.json", "sha256:placeholder")],
                "blocked")));

        Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, error.Message, StringComparison.Ordinal);
        Assert.Contains("outside governed artifact roots", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendEvent_RejectsTraversalThatEscapesGovernedRoots()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new EffectLedgerService(workspace.Paths);
        var escapedPath = workspace.WriteFile(".ai/runtime/../unguarded/traversal.json", "{}");

        var ledgerPath = service.GetWorkOrderLedgerPath("WO-P7-TRAVERSAL");
        var error = Assert.Throws<InvalidOperationException>(() => service.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-WO-P7-TRAVERSAL",
                "unsafe_output",
                "host_admission",
                ["bind_traversal_output"],
                ["bind_traversal_output"],
                [new EffectLedgerOutput("traversal", ".ai/runtime/../unguarded/traversal.json", "sha256:placeholder")],
                "blocked")));

        Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, error.Message, StringComparison.Ordinal);
        Assert.Contains("outside governed artifact roots", error.Message, StringComparison.Ordinal);
    }
}
