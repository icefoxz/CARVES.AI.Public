using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private TaskTruthTransitionCertificateGate EnforceTaskTruthTransitionCertificate(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        ResultEnvelope envelope,
        string resultPath,
        ExecutionBoundaryArtifactSet boundaryArtifacts,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        string? leaseId = null)
    {
        if (originalTask.Status == nextTask.Status)
        {
            return new TaskTruthTransitionCertificateGate(nextTask, null);
        }

        var operation = governedTruthTransitionProfileService.ResolveTaskStatusTransitionOperation(nextTask.Status);
        var requiredOperations = reviewSubmissionAttempt.Created
            && string.Equals(operation, "task_status_to_review", StringComparison.Ordinal)
            ? new[] { "review_submission_recorded", operation }
            : new[] { operation };
        var requiredTransitions = governedTruthTransitionProfileService.BuildTaskTruthTransitions(
            originalTask,
            nextTask,
            operation,
            reviewSubmissionAttempt.Created ? reviewSubmissionAttempt.Submission?.SubmissionId : null);
        var certificatePath = reviewSubmissionAttempt.StateTransitionCertificatePath;
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            var issued = IssueTaskTruthTransitionCertificate(
                originalTask,
                nextTask,
                run,
                envelope,
                resultPath,
                boundaryArtifacts,
                operation,
                leaseId);
            if (!issued.CanIssue)
            {
                throw new InvalidOperationException(
                    issued.FailureMessage
                    ?? $"Cannot transition {originalTask.TaskId} from {originalTask.Status} to {nextTask.Status}: state transition certificate was rejected.");
            }

            certificatePath = issued.CertificatePath;
        }

        var verification = stateTransitionCertificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = certificatePath,
            RequiredOperations = requiredOperations,
            RequiredTransitions = requiredTransitions,
            ExpectedWorkOrderId = $"result-ingestion:{run.RunId}",
            ExpectedTaskId = originalTask.TaskId,
            ExpectedRunId = run.RunId,
            ExpectedHostRoute = reviewSubmissionAttempt.Created
                ? GovernedTruthTransitionProfileService.RunToReviewHostRoute
                : GovernedTruthTransitionProfileService.TaskTruthHostRoute,
            ExpectedTerminalState = reviewSubmissionAttempt.Created
                ? "submitted_to_review"
                : nextTask.Status.ToString(),
            ExpectedLeaseId = leaseId,
            RequireSealedLedger = false,
        });
        if (!verification.CanWriteBack)
        {
            throw new InvalidOperationException(
                verification.FailureMessage
                ?? $"Cannot transition {originalTask.TaskId} from {originalTask.Status} to {nextTask.Status}: state transition certificate is missing or invalid.");
        }

        return new TaskTruthTransitionCertificateGate(nextTask, verification.Certificate!);
    }

    private StateTransitionCertificateIssueResult IssueTaskTruthTransitionCertificate(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        ResultEnvelope envelope,
        string resultPath,
        ExecutionBoundaryArtifactSet boundaryArtifacts,
        string operation,
        string? leaseId)
    {
        var effectLedgerService = new EffectLedgerService(paths);
        var ledgerPath = effectLedgerService.GetRunLedgerPath(run.RunId);
        var resultRelativePath = effectLedgerService.ToRepoRelative(resultPath);
        var resultHash = effectLedgerService.HashFile(resultPath);
        var outputs = new List<EffectLedgerOutput>
        {
            effectLedgerService.BuildOutput("result_envelope", resultRelativePath, resultHash),
        };

        var boundaryDecisionPath = NormalizeOptionalPath(boundaryArtifacts.DecisionPath);
        if (!string.IsNullOrWhiteSpace(boundaryDecisionPath))
        {
            var fullBoundaryDecisionPath = Path.Combine(paths.RepoRoot, boundaryDecisionPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullBoundaryDecisionPath))
            {
                outputs.Add(effectLedgerService.BuildOutput(
                    "boundary_decision",
                    boundaryDecisionPath,
                    effectLedgerService.HashFile(fullBoundaryDecisionPath)));
            }
        }

        var transitionEvent = effectLedgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                $"EV-{run.RunId}",
                "task_truth_transition_authorized",
                nameof(ResultIngestionService),
                ["authorize_task_truth_transition", operation],
                ["authorize_task_truth_transition"],
                outputs,
                "authorized")
            {
                WorkOrderId = $"result-ingestion:{run.RunId}",
                TaskId = originalTask.TaskId,
                RunId = run.RunId,
                LeaseId = leaseId,
                TerminalState = nextTask.Status.ToString(),
                Facts = new Dictionary<string, string?>
                {
                    ["authorized_operation"] = operation,
                    ["task_status_from"] = originalTask.Status.ToString(),
                    ["task_status_to"] = nextTask.Status.ToString().ToUpperInvariant(),
                    ["result_status"] = envelope.Status,
                    ["result_stop_reason"] = envelope.Result.StopReason,
                },
            });

        var requiredEvidence = new List<StateTransitionCertificateEvidence>
        {
            new()
            {
                Kind = "result_envelope",
                Path = resultRelativePath,
                Hash = resultHash,
                Required = true,
            },
            new()
            {
                Kind = "effect_ledger_event",
                Path = effectLedgerService.ToRepoRelative(ledgerPath),
                Hash = transitionEvent.EventHash,
                Required = true,
            },
        };
        if (!string.IsNullOrWhiteSpace(boundaryDecisionPath))
        {
            requiredEvidence.Add(stateTransitionCertificateService.BuildEvidence(
                "boundary_decision",
                boundaryDecisionPath,
                required: true));
        }

        var certificateIssue = stateTransitionCertificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = $"STC-{run.RunId}",
            CertificatePath = stateTransitionCertificateService.GetRunCertificatePath(run.RunId),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = GovernedTruthTransitionProfileService.TaskTruthHostRoute,
            TaskId = originalTask.TaskId,
            RunId = run.RunId,
            WorkOrderId = $"result-ingestion:{run.RunId}",
            LeaseId = leaseId,
            TerminalState = nextTask.Status.ToString(),
            Transitions = governedTruthTransitionProfileService.BuildTaskTruthTransitions(
                originalTask,
                nextTask,
                operation),
            RequiredEvidence = requiredEvidence,
            PolicyVerdict = "allow",
            EffectLedgerPath = effectLedgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = transitionEvent.EventHash,
        });
        if (!certificateIssue.CanIssue)
        {
            return certificateIssue;
        }

        _ = effectLedgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                $"EV-{run.RunId}",
                "state_transition_certificate",
                nameof(ResultIngestionService),
                ["issue_state_transition_certificate"],
                ["issue_state_transition_certificate"],
                [],
                "certified")
            {
                WorkOrderId = $"result-ingestion:{run.RunId}",
                TaskId = originalTask.TaskId,
                RunId = run.RunId,
                LeaseId = leaseId,
                TerminalState = nextTask.Status.ToString(),
                Facts = new Dictionary<string, string?>
                {
                    ["certificate_id"] = certificateIssue.Certificate!.CertificateId,
                    ["certificate_hash"] = certificateIssue.CertificateHash,
                    ["certified_operations"] = operation,
                },
            });
        return certificateIssue;
    }

    private TaskNode RecordTaskTruthTransitionCommitted(
        TaskNode originalTask,
        TaskNode committedTask,
        ExecutionRun run,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        StateTransitionCertificateRecord? prewriteCertificate)
    {
        if (originalTask.Status == committedTask.Status)
        {
            return committedTask;
        }

        var operation = governedTruthTransitionProfileService.ResolveTaskStatusTransitionOperation(committedTask.Status);
        var requiredOperations = reviewSubmissionAttempt.Created
            && string.Equals(operation, "task_status_to_review", StringComparison.Ordinal)
            ? new[] { "review_submission_recorded", operation }
            : new[] { operation };
        var requiredTransitions = governedTruthTransitionProfileService.BuildTaskTruthTransitions(
            originalTask,
            committedTask,
            operation,
            reviewSubmissionAttempt.Created ? reviewSubmissionAttempt.Submission?.SubmissionId : null);
        if (prewriteCertificate is null
            || string.IsNullOrWhiteSpace(prewriteCertificate.CertificatePath))
        {
            throw new InvalidOperationException(
                $"Cannot seal task truth transition for {originalTask.TaskId}: state transition certificate metadata is missing.");
        }

        var effectLedgerService = new EffectLedgerService(paths);
        var ledgerPath = string.IsNullOrWhiteSpace(reviewSubmissionAttempt.EffectLedgerPath)
            ? effectLedgerService.GetRunLedgerPath(run.RunId)
            : Path.Combine(paths.RepoRoot, reviewSubmissionAttempt.EffectLedgerPath.Replace('/', Path.DirectorySeparatorChar));
        var receiptPath = Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "task-truth-writeback-receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        var receipt = new
        {
            schema = "carves.task_truth_writeback_receipt.v0.98-rc.p6",
            task_id = originalTask.TaskId,
            run_id = run.RunId,
            operation,
            from = originalTask.Status.ToString(),
            to = committedTask.Status.ToString().ToUpperInvariant(),
            committed_status = committedTask.Status.ToString(),
            authorization_certificate_path = prewriteCertificate.CertificatePath,
            authorization_certificate_hash = prewriteCertificate.CertificateHash,
            committed_at_utc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions));
        var receiptRelativePath = effectLedgerService.ToRepoRelative(receiptPath);
        var receiptHash = effectLedgerService.HashFile(receiptPath);
        var commitFacts = new Dictionary<string, string?>
        {
            ["task_status_from"] = originalTask.Status.ToString(),
            ["task_status_to"] = committedTask.Status.ToString().ToUpperInvariant(),
            ["authorization_certificate_path"] = prewriteCertificate.CertificatePath,
            ["receipt_path"] = receiptRelativePath,
        };
        if (reviewSubmissionAttempt.Created
            && reviewSubmissionAttempt.Submission is not null
            && string.Equals(operation, "task_status_to_review", StringComparison.Ordinal))
        {
            commitFacts["review_submission_id"] = reviewSubmissionAttempt.Submission.SubmissionId;
            commitFacts["review_submission_from"] = "absent";
            commitFacts["review_submission_to"] = "recorded";
        }

        var commitEvent = effectLedgerService.AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                $"EV-{run.RunId}",
                "task_truth_transition_committed",
                nameof(ResultIngestionService),
                [operation],
                [operation],
                [effectLedgerService.BuildOutput("task_truth_writeback_receipt", receiptRelativePath, receiptHash)],
                "committed")
            {
                WorkOrderId = prewriteCertificate.WorkOrderId,
                TaskId = originalTask.TaskId,
                RunId = run.RunId,
                LeaseId = prewriteCertificate.LeaseId,
                TerminalState = reviewSubmissionAttempt.Created
                    ? "submitted_to_review"
                    : committedTask.Status.ToString(),
                Facts = commitFacts,
            });
        _ = effectLedgerService.Seal(
            ledgerPath,
            new EffectLedgerSealDraft($"EV-{run.RunId}", nameof(ResultIngestionService))
            {
                WorkOrderId = prewriteCertificate.WorkOrderId,
                TaskId = originalTask.TaskId,
                RunId = run.RunId,
                LeaseId = prewriteCertificate.LeaseId,
                TerminalState = reviewSubmissionAttempt.Created
                    ? "submitted_to_review"
                    : committedTask.Status.ToString(),
                Facts = new Dictionary<string, string?>
                {
                    ["committed_event_hash"] = commitEvent.EventHash,
                    ["committed_operation"] = operation,
                    ["receipt_path"] = receiptRelativePath,
                },
            });

        var reboundCertificate = stateTransitionCertificateService.RebindCommittedEffect(new StateTransitionCertificateRebindRequest
        {
            CertificatePath = prewriteCertificate.CertificatePath,
            EffectLedgerPath = effectLedgerService.ToRepoRelative(ledgerPath),
            EffectLedgerEventHash = commitEvent.EventHash,
            ExpectedWorkOrderId = prewriteCertificate.WorkOrderId,
            ExpectedTaskId = originalTask.TaskId,
            ExpectedRunId = run.RunId,
            ExpectedHostRoute = reviewSubmissionAttempt.Created
                ? GovernedTruthTransitionProfileService.RunToReviewHostRoute
                : GovernedTruthTransitionProfileService.TaskTruthHostRoute,
            ExpectedTerminalState = reviewSubmissionAttempt.Created
                ? "submitted_to_review"
                : committedTask.Status.ToString(),
            ExpectedLeaseId = prewriteCertificate.LeaseId,
            AdditionalEvidence =
            [
                stateTransitionCertificateService.BuildEvidence(
                    "task_truth_writeback_receipt",
                    receiptRelativePath,
                    required: true),
            ],
        });
        if (!reboundCertificate.CanIssue)
        {
            throw new InvalidOperationException(
                reboundCertificate.FailureMessage
                ?? $"Cannot seal task truth transition for {originalTask.TaskId}: state transition certificate could not be rebound to the committed effect.");
        }

        var verification = stateTransitionCertificateService.VerifyRequired(new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = reboundCertificate.CertificatePath,
            RequiredOperations = requiredOperations,
            RequiredTransitions = requiredTransitions,
            ExpectedWorkOrderId = prewriteCertificate.WorkOrderId,
            ExpectedTaskId = originalTask.TaskId,
            ExpectedRunId = run.RunId,
            ExpectedHostRoute = reviewSubmissionAttempt.Created
                ? GovernedTruthTransitionProfileService.RunToReviewHostRoute
                : GovernedTruthTransitionProfileService.TaskTruthHostRoute,
            ExpectedTerminalState = reviewSubmissionAttempt.Created
                ? "submitted_to_review"
                : committedTask.Status.ToString(),
            ExpectedLeaseId = prewriteCertificate.LeaseId,
            RequireSealedLedger = true,
        });
        if (!verification.CanWriteBack)
        {
            throw new InvalidOperationException(
                verification.FailureMessage
                ?? $"Cannot seal task truth transition for {originalTask.TaskId}: state transition certificate failed sealed ledger verification.");
        }

        return ApplyTaskTruthTransitionCommitMetadata(
            committedTask,
            verification.Certificate!,
            receiptRelativePath,
            receiptHash,
            commitEvent.EventHash);
    }

    private static TaskNode ApplyTaskTruthTransitionCertificateMetadata(
        TaskNode task,
        StateTransitionCertificateRecord certificate)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["task_truth_transition_certificate_path"] = certificate.CertificatePath,
            ["task_truth_transition_certificate_hash"] = certificate.CertificateHash,
            ["task_truth_transition_certified_transitions"] = string.Join(",", certificate.Transitions.Select(static transition => transition.Operation)),
            ["task_truth_transition_certificate_host_route"] = certificate.HostRoute,
            ["task_truth_transition_effect_ledger_path"] = certificate.EffectLedgerPath,
            ["task_truth_transition_effect_ledger_event_hash"] = certificate.EffectLedgerEventHash,
        };

        return Clone(task, task.Status, task.PlannerReview, metadata, task.AcceptanceContract);
    }

    private static TaskNode ApplyTaskTruthTransitionAuthorizationMetadata(
        TaskNode task,
        StateTransitionCertificateRecord? certificate)
    {
        if (certificate is null)
        {
            return task;
        }

        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["task_truth_transition_authorization_certificate_path"] = certificate.CertificatePath,
            ["task_truth_transition_authorization_certificate_hash"] = certificate.CertificateHash,
            ["task_truth_transition_authorization_certified_transitions"] = string.Join(",", certificate.Transitions.Select(static transition => transition.Operation)),
            ["task_truth_transition_authorization_certificate_host_route"] = certificate.HostRoute,
            ["task_truth_transition_authorization_effect_ledger_path"] = certificate.EffectLedgerPath,
            ["task_truth_transition_authorization_effect_ledger_event_hash"] = certificate.EffectLedgerEventHash,
        };

        return Clone(task, task.Status, task.PlannerReview, metadata, task.AcceptanceContract);
    }

    private static TaskNode ApplyTaskTruthTransitionCommitMetadata(
        TaskNode task,
        StateTransitionCertificateRecord certificate,
        string receiptPath,
        string receiptHash,
        string committedEventHash)
    {
        var withCertificate = ApplyTaskTruthTransitionCertificateMetadata(task, certificate);
        var metadata = new Dictionary<string, string>(withCertificate.Metadata, StringComparer.Ordinal)
        {
            ["task_truth_transition_receipt_path"] = receiptPath,
            ["task_truth_transition_receipt_hash"] = receiptHash,
            ["task_truth_transition_committed_event_hash"] = committedEventHash,
        };
        metadata.Remove("task_truth_transition_authorization_certificate_path");
        metadata.Remove("task_truth_transition_authorization_certificate_hash");
        metadata.Remove("task_truth_transition_authorization_certified_transitions");
        metadata.Remove("task_truth_transition_authorization_certificate_host_route");
        metadata.Remove("task_truth_transition_authorization_effect_ledger_path");
        metadata.Remove("task_truth_transition_authorization_effect_ledger_event_hash");
        if (metadata.ContainsKey("review_submission_state_transition_certificate_path"))
        {
            metadata["review_submission_state_transition_certificate_path"] = certificate.CertificatePath;
            metadata["review_submission_state_transition_certificate_hash"] = certificate.CertificateHash;
        }

        return Clone(withCertificate, withCertificate.Status, withCertificate.PlannerReview, metadata, withCertificate.AcceptanceContract);
    }

    private string? NormalizeOptionalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? NormalizeRepoRelative(path)
            : path.Replace('\\', '/');
    }

    private sealed record TaskTruthTransitionCertificateGate(
        TaskNode Task,
        StateTransitionCertificateRecord? Certificate);
}
