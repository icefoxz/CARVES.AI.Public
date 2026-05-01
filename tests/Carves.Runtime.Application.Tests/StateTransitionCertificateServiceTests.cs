using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class StateTransitionCertificateServiceTests
{
    [Fact]
    public void TryIssue_CreatesVerifiableCertificateForReviewTransition()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-001");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-001/review-submission.json", "{\"submitted\":true}");
        var boundaryPath = workspace.WriteFile(".ai/runtime/boundary/decisions/T-P6-001.json", "{\"decision\":\"allow\"}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-001",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-001",
                RunId = "run-p6-001",
                LeaseId = "CL-P6-001",
                TransactionHash = "sha256:transaction",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["review_submission_id"] = "RTREV-run-p6-001",
                    ["review_submission_from"] = "absent",
                    ["review_submission_to"] = "recorded",
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var result = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-001",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-001"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-001",
            RunId = "run-p6-001",
            LeaseId = "CL-P6-001",
            ExpectedLeaseId = "CL-P6-001",
            TransactionHash = "sha256:transaction",
            ExpectedTransactionHash = "sha256:transaction",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/artifacts/worker-executions/",
                    Operation = "review_submission_recorded",
                    ObjectId = "RTREV-run-p6-001",
                    From = "absent",
                    To = "recorded",
                },
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-001",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                certificateService.BuildEvidence("boundary_decision", boundaryPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });

        Assert.True(result.CanIssue);
        Assert.NotNull(result.Certificate);
        Assert.Equal("carves.state_transition_certificate.v0.98-rc.p6", result.Certificate!.Schema);
        Assert.Equal("runtime-control-plane", result.Certificate.Issuer);
        Assert.Equal("host.result_ingestion.run_to_review", result.Certificate.HostRoute);
        Assert.Equal("allow", result.Certificate.PolicyVerdict);
        Assert.Equal("submitted_to_review", result.Certificate.TerminalState);
        Assert.Equal(2, result.Certificate.Transitions.Count);
        Assert.Contains(result.Certificate.Transitions, item => item.Operation == "task_status_to_review");
        Assert.Contains(result.Certificate.Transitions, item => item.Operation == "review_submission_recorded");
        Assert.False(string.IsNullOrWhiteSpace(result.Certificate.CertificateHash));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.CertificatePath!.Replace('/', Path.DirectorySeparatorChar))));
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-001", result);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = result.CertificatePath,
            RequiredOperations = ["review_submission_recorded", "task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/artifacts/worker-executions/",
                    Operation = "review_submission_recorded",
                    ObjectId = "RTREV-run-p6-001",
                    From = "absent",
                    To = "recorded",
                },
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-001",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedTaskId = "T-P6-001",
            ExpectedRunId = "run-p6-001",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
            ExpectedLeaseId = "CL-P6-001",
            ExpectedTransactionHash = "sha256:transaction",
        });
        Assert.True(verification.CanWriteBack);
        Assert.Equal("STC-run-p6-001", verification.Certificate!.CertificateId);
    }

    [Fact]
    public void TryIssue_RejectsMissingRequiredEvidence()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-missing/effect-ledger.jsonl", "{}");

        var result = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-missing",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-missing"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-MISSING",
            RunId = "run-p6-missing",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-MISSING",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                new StateTransitionCertificateEvidence
                {
                    Kind = "boundary_decision",
                    Path = ".ai/runtime/boundary/decisions/missing.json",
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = "sha256:event",
        });

        Assert.False(result.CanIssue);
        Assert.Contains(StateTransitionCertificateService.MissingCertificateStopReason, result.StopReasons);
        Assert.False(File.Exists(certificateService.GetRunCertificatePath("run-p6-missing")));
    }

    [Fact]
    public void TryIssue_RejectsStaleLeaseAndTransactionHashes()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var evidencePath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-stale/review-submission.json", "{}");
        var ledgerPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-stale/effect-ledger.jsonl", "{}");

        var result = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-stale",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-stale"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-STALE",
            RunId = "run-p6-stale",
            LeaseId = "CL-ACTUAL",
            ExpectedLeaseId = "CL-EXPECTED",
            TransactionHash = "sha256:actual",
            ExpectedTransactionHash = "sha256:expected",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-STALE",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", evidencePath),
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = "sha256:event",
        });

        Assert.False(result.CanIssue);
        Assert.Contains(StateTransitionCertificateService.StaleCertificateStopReason, result.StopReasons);
        Assert.False(File.Exists(certificateService.GetRunCertificatePath("run-p6-stale")));
    }

    [Fact]
    public void VerifyRequired_RejectsTamperedCertificatePayloadEvenWhenEvidenceStillMatches()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-tampered");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-tampered/review-submission.json", "{\"submitted\":true}");
        var boundaryPath = workspace.WriteFile(".ai/runtime/boundary/decisions/T-P6-TAMPERED.json", "{\"decision\":\"allow\"}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-tampered",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-TAMPERED",
                RunId = "run-p6-tampered",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-tampered",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-tampered"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-TAMPERED",
            RunId = "run-p6-tampered",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-TAMPERED",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                certificateService.BuildEvidence("boundary_decision", boundaryPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-tampered", issue);

        var certificatePath = Path.Combine(workspace.RootPath, issue.CertificatePath!.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(
            certificatePath,
            File.ReadAllText(certificatePath).Replace("\"to\": \"REVIEW\"", "\"to\": \"COMPLETED\"", StringComparison.Ordinal));

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-TAMPERED",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedTaskId = "T-P6-TAMPERED",
            ExpectedRunId = "run-p6-tampered",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.RejectedCertificateStopReason, verification.StopReasons);
        Assert.Contains("certificate_hash", verification.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyRequired_RejectsCertificateForDifferentTaskEvenWhenOperationMatches()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-object-a");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-object-a/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-object-a",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-A",
                RunId = "run-p6-object-a",
                TerminalState = "submitted_to_review",
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-object-a",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-object-a"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-A",
            RunId = "run-p6-object-a",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-A",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-object-a", issue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-B",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedTaskId = "T-P6-B",
            ExpectedRunId = "run-p6-object-a",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.ContextMismatchStopReason, verification.StopReasons);
    }

    [Fact]
    public void VerifyRequired_RejectsCertificateWhenExpectedWorkOrderDoesNotMatch()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-work-order-mismatch");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-work-order-mismatch/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-work-order-mismatch",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                WorkOrderId = "WO-P6-A",
                TaskId = "T-P6-WO",
                RunId = "run-p6-work-order-mismatch",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-work-order-mismatch",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-work-order-mismatch"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            WorkOrderId = "WO-P6-A",
            TaskId = "T-P6-WO",
            RunId = "run-p6-work-order-mismatch",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-WO",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-work-order-mismatch", issue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-WO",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedWorkOrderId = "WO-P6-B",
            ExpectedTaskId = "T-P6-WO",
            ExpectedRunId = "run-p6-work-order-mismatch",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.ContextMismatchStopReason, verification.StopReasons);
        Assert.Contains("work_order_id", verification.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyRequired_RejectsCertificateWhenLedgerReplayWorkOrderDiffersFromCertificate()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-ledger-work-order");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-ledger-work-order/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-ledger-work-order",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                WorkOrderId = "WO-P6-LEDGER",
                TaskId = "T-P6-LEDGER-WO",
                RunId = "run-p6-ledger-work-order",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-ledger-work-order",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-ledger-work-order"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            WorkOrderId = "WO-P6-CERT",
            TaskId = "T-P6-LEDGER-WO",
            RunId = "run-p6-ledger-work-order",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-LEDGER-WO",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-ledger-work-order", issue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-LEDGER-WO",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedWorkOrderId = "WO-P6-CERT",
            ExpectedTaskId = "T-P6-LEDGER-WO",
            ExpectedRunId = "run-p6-ledger-work-order",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.LedgerContextMismatchStopReason, verification.StopReasons);
        Assert.Contains("work_order_id", verification.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyRequired_RejectsCertificateWhenLedgerEventFactsDoNotMatchTransition()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-ledger-facts");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-ledger-facts/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-ledger-facts",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-FACTS",
                RunId = "run-p6-ledger-facts",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "COMPLETED",
                },
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-ledger-facts",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-ledger-facts"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-FACTS",
            RunId = "run-p6-ledger-facts",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-FACTS",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-ledger-facts", issue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-FACTS",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedTaskId = "T-P6-FACTS",
            ExpectedRunId = "run-p6-ledger-facts",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.LedgerContextMismatchStopReason, verification.StopReasons);
        Assert.Contains("task_status_to", verification.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRequired_RejectsCertificateWhenEffectLedgerIsNotSealed()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-unsealed");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-unsealed/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-unsealed",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-UNSEALED",
                RunId = "run-p6-unsealed",
                TerminalState = "submitted_to_review",
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-unsealed",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-unsealed"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-UNSEALED",
            RunId = "run-p6-unsealed",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-UNSEALED",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            RequiredTransitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-UNSEALED",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            ExpectedTaskId = "T-P6-UNSEALED",
            ExpectedRunId = "run-p6-unsealed",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(EffectLedgerService.AuditIncompleteStopReason, verification.StopReasons);
    }

    [Fact]
    public void RebindCommittedEffect_RejectsLeaseMismatchForCommittedTransition()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-rebind-lease");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-rebind-lease/review-submission.json", "{\"submitted\":true}");
        var authorizationEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-rebind-lease",
                "task_truth_transition_authorized",
                "result_ingestion",
                ["authorize_task_truth_transition", "task_status_to_review"],
                ["authorize_task_truth_transition"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "authorized")
            {
                TaskId = "T-P6-REBIND-LEASE",
                RunId = "run-p6-rebind-lease",
                LeaseId = "CL-P6-ACTUAL",
                TerminalState = "Review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var issued = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-rebind-lease",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-rebind-lease"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.task_truth_transition",
            TaskId = "T-P6-REBIND-LEASE",
            RunId = "run-p6-rebind-lease",
            WorkOrderId = "result-ingestion:run-p6-rebind-lease",
            LeaseId = "CL-P6-ACTUAL",
            TerminalState = "Review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-REBIND-LEASE",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = authorizationEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = authorizationEvent.EventHash,
        });
        Assert.True(issued.CanIssue);

        var committedEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-rebind-lease",
                "task_truth_transition_committed",
                "result_ingestion",
                ["task_status_to_review"],
                ["task_status_to_review"],
                [],
                "committed")
            {
                WorkOrderId = "result-ingestion:run-p6-rebind-lease",
                TaskId = "T-P6-REBIND-LEASE",
                RunId = "run-p6-rebind-lease",
                LeaseId = "CL-P6-ACTUAL",
                TerminalState = "Review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var rebound = certificateService.RebindCommittedEffect(new StateTransitionCertificateRebindRequest
        {
            CertificatePath = issued.CertificatePath!,
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = committedEvent.EventHash,
            ExpectedTaskId = "T-P6-REBIND-LEASE",
            ExpectedRunId = "run-p6-rebind-lease",
            ExpectedHostRoute = "host.result_ingestion.task_truth_transition",
            ExpectedTerminalState = "Review",
            ExpectedLeaseId = "CL-P6-EXPECTED",
        });

        Assert.False(rebound.CanIssue);
        Assert.Contains(StateTransitionCertificateService.ContextMismatchStopReason, rebound.StopReasons);
        Assert.Contains("lease_id", rebound.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyRequired_RejectsVerificationRequestWithoutRequiredTransitionDescriptors()
    {
        using var workspace = new TemporaryWorkspace();
        var ledgerService = new EffectLedgerService(workspace.Paths);
        var certificateService = new StateTransitionCertificateService(workspace.Paths, ledgerService);
        var ledgerPath = ledgerService.GetRunLedgerPath("run-p6-no-descriptor");
        var reviewSubmissionPath = workspace.WriteFile(".ai/artifacts/worker-executions/run-p6-no-descriptor/review-submission.json", "{\"submitted\":true}");
        var ledgerEvent = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                "EV-run-p6-no-descriptor",
                "submit_to_review",
                "result_ingestion",
                ["create_review_submission_sidecar"],
                ["create_review_submission_sidecar"],
                [ledgerService.BuildOutput("review_submission", reviewSubmissionPath, ledgerService.HashFile(reviewSubmissionPath))],
                "submitted_to_review")
            {
                TaskId = "T-P6-NO-DESCRIPTOR",
                RunId = "run-p6-no-descriptor",
                TerminalState = "submitted_to_review",
                Facts = new Dictionary<string, string?>
                {
                    ["task_status_from"] = "Pending",
                    ["task_status_to"] = "REVIEW",
                },
            });

        var issue = certificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = "STC-run-p6-no-descriptor",
            CertificatePath = certificateService.GetRunCertificatePath("run-p6-no-descriptor"),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = "host.result_ingestion.run_to_review",
            TaskId = "T-P6-NO-DESCRIPTOR",
            RunId = "run-p6-no-descriptor",
            TerminalState = "submitted_to_review",
            Transitions =
            [
                new StateTransitionOperation
                {
                    Root = ".ai/tasks/",
                    Operation = "task_status_to_review",
                    ObjectId = "T-P6-NO-DESCRIPTOR",
                    From = "Pending",
                    To = "REVIEW",
                },
            ],
            RequiredEvidence =
            [
                certificateService.BuildEvidence("review_submission_record", reviewSubmissionPath),
                new StateTransitionCertificateEvidence
                {
                    Kind = "effect_ledger_event",
                    Path = ledgerService.ToRepoRelative(ledgerPath),
                    Hash = ledgerEvent.EventHash,
                    Required = true,
                },
            ],
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = ledgerEvent.EventHash,
        });
        Assert.True(issue.CanIssue);
        SealLedgerForCertificate(ledgerService, ledgerPath, "run-p6-no-descriptor", issue);

        var verification = certificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = issue.CertificatePath,
            RequiredOperations = ["task_status_to_review"],
            ExpectedTaskId = "T-P6-NO-DESCRIPTOR",
            ExpectedRunId = "run-p6-no-descriptor",
            ExpectedHostRoute = "host.result_ingestion.run_to_review",
            ExpectedTerminalState = "submitted_to_review",
        });

        Assert.False(verification.CanWriteBack);
        Assert.Contains(StateTransitionCertificateService.TransitionMismatchStopReason, verification.StopReasons);
        Assert.Contains("required_transitions", verification.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static void SealLedgerForCertificate(
        EffectLedgerService ledgerService,
        string ledgerPath,
        string runId,
        StateTransitionCertificateIssueResult issue)
    {
        _ = ledgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                $"EV-{runId}",
                "state_transition_certificate",
                "result_ingestion",
                ["issue_state_transition_certificate"],
                ["issue_state_transition_certificate"],
                [ledgerService.BuildOutput("state_transition_certificate", issue.CertificatePath!, issue.CertificateHash!)],
                "certified")
            {
                TaskId = issue.Certificate!.TaskId,
                RunId = runId,
                TerminalState = issue.Certificate.TerminalState,
            });
        _ = ledgerService.Seal(
            ledgerPath,
            new EffectLedgerSealDraft($"EV-{runId}", "result_ingestion")
            {
                TaskId = issue.Certificate.TaskId,
                RunId = runId,
                TerminalState = issue.Certificate.TerminalState,
            });
    }
}
