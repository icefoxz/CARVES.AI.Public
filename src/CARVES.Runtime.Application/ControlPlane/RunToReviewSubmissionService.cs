using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class RunToReviewSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IGitClient? gitClient;
    private readonly EffectLedgerService effectLedgerService;
    private readonly IStateTransitionCertificateService stateTransitionCertificateService;
    private readonly GovernedTruthTransitionProfileService governedTruthTransitionProfileService;

    public RunToReviewSubmissionService(
        ControlPlanePaths paths,
        IGitClient? gitClient = null,
        EffectLedgerService? effectLedgerService = null,
        IStateTransitionCertificateService? stateTransitionCertificateService = null)
    {
        this.paths = paths;
        this.gitClient = gitClient;
        this.effectLedgerService = effectLedgerService ?? new EffectLedgerService(paths);
        this.stateTransitionCertificateService = stateTransitionCertificateService
            ?? new StateTransitionCertificateService(paths, this.effectLedgerService);
        this.governedTruthTransitionProfileService = new GovernedTruthTransitionProfileService();
    }

    public RunToReviewSubmissionAttempt TryCreate(
        TaskNode task,
        ExecutionRun run,
        ResultEnvelope envelope,
        WorkerExecutionArtifact workerArtifact,
        BoundaryDecision decision,
        ExecutionBoundaryArtifactSet boundaryArtifacts)
    {
        if (decision.WritebackDecision is not BoundaryWritebackDecision.AdmitToReview
            and not BoundaryWritebackDecision.RequireHumanReview)
        {
            return RunToReviewSubmissionAttempt.NotApplicable();
        }

        var evidence = workerArtifact.Evidence ?? ExecutionEvidence.None;
        if (evidence.EvidenceCompleteness != ExecutionEvidenceCompleteness.Complete)
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot submit {task.TaskId} to review: execution evidence is {evidence.EvidenceCompleteness}.");
        }

        if (evidence.EvidenceStrength < ExecutionEvidenceStrength.Verifiable)
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot submit {task.TaskId} to review: execution evidence is only {evidence.EvidenceStrength}.");
        }

        if (string.Equals(envelope.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot submit {task.TaskId} to review: verification did not pass.");
        }

        var runEvidenceRoot = GetRunEvidenceRoot(run.RunId);
        Directory.CreateDirectory(runEvidenceRoot);

        var resultCommit = CaptureResultCommit(task.TaskId, evidence);
        if (!resultCommit.CanProceed)
        {
            return RunToReviewSubmissionAttempt.Block(resultCommit.FailureMessage!);
        }

        var submissionPath = GetPreparedSubmissionPath(run.RunId);
        var ledgerPath = effectLedgerService.GetRunLedgerPath(run.RunId);
        var submissionRelativePath = effectLedgerService.ToRepoRelative(submissionPath);
        var ledgerRelativePath = effectLedgerService.ToRepoRelative(ledgerPath);
        var submission = new RunToReviewSubmissionRecord
        {
            SubmissionId = $"RTREV-{run.RunId}",
            TaskId = task.TaskId,
            CardId = task.CardId,
            RunId = run.RunId,
            WorkerRunId = workerArtifact.Result.RunId,
            TerminalState = "submitted_to_review",
            TaskStateOnSuccess = "REVIEW",
            ReviewVerdictWritten = false,
            TaskCompleted = false,
            ResultCommit = resultCommit.Commit,
            ResultCommitStatus = resultCommit.Status,
            ResultCommitPolicy = "capture_if_git_worktree_after_boundary_allow",
            WorktreePath = evidence.WorktreePath,
            EvidencePath = evidence.EvidencePath,
            CommandLogPath = evidence.CommandLogRef,
            BuildLogPath = evidence.BuildOutputRef,
            TestLogPath = evidence.TestOutputRef,
            PatchPath = evidence.PatchRef,
            BoundaryDecisionPath = NormalizeArtifactPath(boundaryArtifacts.DecisionPath),
            BoundaryWritebackDecision = decision.WritebackDecision.ToString(),
            BoundaryReasonCodes = decision.ReasonCodes,
            RequiredEvidence = ResolveRequiredEvidence(envelope, evidence),
            EffectLedgerPath = ledgerRelativePath,
            ReceiptSummary = BuildReceiptSummary(task.TaskId, run.RunId, resultCommit),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        try
        {
            var submissionPayload = JsonSerializer.Serialize(submission, JsonOptions);
            File.WriteAllText(submissionPath, submissionPayload);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot prepare {task.TaskId} for review submission: pending submission sidecar write failed ({ex.Message}).");
        }

        return RunToReviewSubmissionAttempt.Prepare(
            submission,
            submissionRelativePath,
            ledgerRelativePath,
            resultCommit.Commit);
    }

    public string GetSubmissionPath(string runId)
    {
        return Path.Combine(GetRunEvidenceRoot(runId), "review-submission.json");
    }

    public string GetPreparedSubmissionPath(string runId)
    {
        return Path.Combine(GetRunEvidenceRoot(runId), "review-submission.pending.json");
    }

    public string GetEffectLedgerPath(string runId)
    {
        return effectLedgerService.GetRunLedgerPath(runId);
    }

    public RunToReviewSubmissionAttempt CommitPrepared(
        TaskNode task,
        ExecutionRun run,
        RunToReviewSubmissionAttempt preparedAttempt,
        string leaseId)
    {
        if (!preparedAttempt.Created || preparedAttempt.Submission is null)
        {
            return preparedAttempt;
        }

        var preparedPath = string.IsNullOrWhiteSpace(preparedAttempt.SubmissionPath)
            ? GetPreparedSubmissionPath(run.RunId)
            : Path.Combine(paths.RepoRoot, preparedAttempt.SubmissionPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(preparedPath))
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot submit {task.TaskId} to review: prepared submission sidecar is missing.");
        }

        var finalSubmissionPath = GetSubmissionPath(run.RunId);
        var finalSubmissionRelativePath = effectLedgerService.ToRepoRelative(finalSubmissionPath);
        var ledgerPath = effectLedgerService.GetRunLedgerPath(run.RunId);
        var ledgerRelativePath = effectLedgerService.ToRepoRelative(ledgerPath);
        StateTransitionCertificateIssueResult certificateIssue;
        EffectLedgerAppendResult certificateEvent;
        try
        {
            var submissionPayload = File.ReadAllText(preparedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalSubmissionPath)!);
            File.WriteAllText(finalSubmissionPath, submissionPayload);
            if (!string.Equals(preparedPath, finalSubmissionPath, StringComparison.Ordinal) && File.Exists(preparedPath))
            {
                File.Delete(preparedPath);
            }

            var submissionHash = effectLedgerService.HashFile(finalSubmissionPath);
            var submissionEvent = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    $"EV-{preparedAttempt.Submission.RunId}",
                    "submit_to_review",
                    nameof(ResultIngestionService),
                    [
                        "create_review_submission_sidecar",
                        "task_status_to_review",
                    ],
                    [
                        "create_review_submission_sidecar",
                    ],
                    [
                        effectLedgerService.BuildOutput("review_submission", finalSubmissionRelativePath, submissionHash),
                    ],
                    "submitted_to_review")
                {
                    WorkOrderId = $"result-ingestion:{run.RunId}",
                    TaskId = preparedAttempt.Submission.TaskId,
                    RunId = preparedAttempt.Submission.RunId,
                    LeaseId = leaseId,
                    TerminalState = preparedAttempt.Submission.TerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["review_submission_id"] = preparedAttempt.Submission.SubmissionId,
                        ["review_submission_from"] = "absent",
                        ["review_submission_to"] = "recorded",
                        ["task_status_from"] = task.Status.ToString(),
                        ["task_status_to"] = "REVIEW",
                        ["result_commit"] = preparedAttempt.ResultCommit,
                        ["result_commit_status"] = preparedAttempt.Submission.ResultCommitStatus,
                        ["review_verdict_written"] = "false",
                        ["task_completed"] = "false",
                    },
                });
            certificateIssue = IssueStateTransitionCertificate(
                task,
                run,
                preparedAttempt.Submission,
                finalSubmissionRelativePath,
                submissionHash,
                ledgerRelativePath,
                submissionEvent.EventHash,
                leaseId);
            if (!certificateIssue.CanIssue)
            {
                return RunToReviewSubmissionAttempt.Block(
                    certificateIssue.FailureMessage
                    ?? $"Cannot submit {task.TaskId} to review: state transition certificate was rejected.");
            }

            certificateEvent = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    $"EV-{preparedAttempt.Submission.RunId}",
                    "state_transition_certificate",
                    nameof(ResultIngestionService),
                    [
                        "issue_state_transition_certificate",
                    ],
                    [
                        "issue_state_transition_certificate",
                    ],
                    [],
                    "certified")
                {
                    WorkOrderId = $"result-ingestion:{run.RunId}",
                    TaskId = preparedAttempt.Submission.TaskId,
                    RunId = preparedAttempt.Submission.RunId,
                    LeaseId = leaseId,
                    TerminalState = preparedAttempt.Submission.TerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["certificate_id"] = certificateIssue.Certificate!.CertificateId,
                        ["certificate_hash"] = certificateIssue.CertificateHash,
                        ["certified_operations"] = string.Join(",", certificateIssue.Certificate.Transitions.Select(static transition => transition.Operation)),
                    },
                });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return RunToReviewSubmissionAttempt.Block(
                $"Cannot submit {task.TaskId} to review: committed submission sidecar write failed ({ex.Message}).");
        }

        return RunToReviewSubmissionAttempt.Commit(
            preparedAttempt.Submission with
            {
                StateTransitionCertificatePath = certificateIssue.CertificatePath ?? string.Empty,
                StateTransitionCertificateHash = certificateIssue.CertificateHash ?? string.Empty,
                CertifiedTransitions = certificateIssue.Certificate?.Transitions
                    .Select(static transition => transition.Operation)
                    .ToArray()
                    ?? [],
            },
            finalSubmissionRelativePath,
            ledgerRelativePath,
            certificateEvent.EventHash,
            preparedAttempt.ResultCommit,
            certificateIssue.CertificatePath,
            certificateIssue.CertificateHash);
    }

    private RunToReviewResultCommit CaptureResultCommit(string taskId, ExecutionEvidence evidence)
    {
        if (gitClient is null)
        {
            return RunToReviewResultCommit.Allow(null, "not_captured_git_client_unavailable");
        }

        if (string.IsNullOrWhiteSpace(evidence.WorktreePath)
            || !Directory.Exists(evidence.WorktreePath))
        {
            return RunToReviewResultCommit.Allow(null, "not_captured_worktree_unavailable");
        }

        if (evidence.FilesWritten.Count == 0)
        {
            return RunToReviewResultCommit.Allow(null, "not_captured_no_changed_files");
        }

        if (!gitClient.IsRepository(evidence.WorktreePath))
        {
            return RunToReviewResultCommit.Allow(null, "not_captured_non_git_worktree");
        }

        var commit = gitClient.TryCreateScopedSnapshotCommit(
            evidence.WorktreePath,
            evidence.FilesWritten,
            $"CARVES L3 result for {taskId}");
        return string.IsNullOrWhiteSpace(commit)
            ? RunToReviewResultCommit.Block(
                $"Cannot submit {taskId} to review: git worktree result commit could not be created.")
            : RunToReviewResultCommit.Allow(commit, "captured");
    }

    private StateTransitionCertificateIssueResult IssueStateTransitionCertificate(
        TaskNode task,
        ExecutionRun run,
        RunToReviewSubmissionRecord submission,
        string submissionPath,
        string submissionHash,
        string ledgerPath,
        string ledgerEventHash,
        string leaseId)
    {
        var boundaryDecisionPath = NormalizeArtifactPath(submission.BoundaryDecisionPath);
        var requiredEvidence = new List<StateTransitionCertificateEvidence>
        {
            new()
            {
                Kind = "review_submission_record",
                Path = submissionPath,
                Hash = submissionHash,
                Required = true,
            },
            new()
            {
                Kind = "effect_ledger_event",
                Path = ledgerPath,
                Hash = ledgerEventHash,
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
        else
        {
            requiredEvidence.Add(new StateTransitionCertificateEvidence
            {
                Kind = "boundary_decision",
                Required = true,
            });
        }

        return stateTransitionCertificateService.TryIssue(new StateTransitionCertificateIssueRequest
        {
            CertificateId = $"STC-{run.RunId}",
            CertificatePath = stateTransitionCertificateService.GetRunCertificatePath(run.RunId),
            Issuer = StateTransitionCertificateService.HostIssuer,
            HostRoute = GovernedTruthTransitionProfileService.RunToReviewHostRoute,
            TaskId = task.TaskId,
            RunId = run.RunId,
            WorkOrderId = $"result-ingestion:{run.RunId}",
            LeaseId = leaseId,
            TerminalState = submission.TerminalState,
            Transitions = governedTruthTransitionProfileService.BuildRunToReviewTransitions(
                task.TaskId,
                task.Status,
                submission.SubmissionId),
            RequiredEvidence = requiredEvidence,
            PolicyVerdict = "allow",
            EffectLedgerPath = ledgerPath,
            EffectLedgerEventHash = ledgerEventHash,
        });
    }

    private string GetRunEvidenceRoot(string runId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId);
    }

    private string ToRepoRelative(string path)
    {
        return effectLedgerService.ToRepoRelative(path);
    }

    private string? NormalizeArtifactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? ToRepoRelative(path)
            : path.Replace('\\', '/');
    }

    private static IReadOnlyList<string> ResolveRequiredEvidence(ResultEnvelope envelope, ExecutionEvidence evidence)
    {
        var required = new List<string>
        {
            "execution_evidence",
            "command_log",
            "patch_summary",
            "boundary_decision",
        };

        if (!string.Equals(envelope.Validation.Build, "not_run", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(evidence.BuildOutputRef))
        {
            required.Add("build_log");
        }

        if (!string.Equals(envelope.Validation.Tests, "not_run", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(evidence.TestOutputRef))
        {
            required.Add("test_result");
        }

        return required;
    }

    private static string BuildReceiptSummary(
        string taskId,
        string runId,
        RunToReviewResultCommit resultCommit)
    {
        var commitSummary = string.IsNullOrWhiteSpace(resultCommit.Commit)
            ? resultCommit.Status
            : $"result_commit={resultCommit.Commit}";
        return $"Submitted {taskId} run {runId} to review with {commitSummary}; no review verdict or completed state was written.";
    }

}

public sealed record RunToReviewSubmissionAttempt(
    bool CanProceed,
    bool Created,
    bool Committed,
    RunToReviewSubmissionRecord? Submission,
    string? SubmissionPath,
    string? EffectLedgerPath,
    string? EffectLedgerEventHash,
    string? ResultCommit,
    string? StateTransitionCertificatePath,
    string? StateTransitionCertificateHash,
    string? FailureMessage)
{
    public static RunToReviewSubmissionAttempt NotApplicable()
    {
        return new RunToReviewSubmissionAttempt(true, false, false, null, null, null, null, null, null, null, null);
    }

    public static RunToReviewSubmissionAttempt Prepare(
        RunToReviewSubmissionRecord submission,
        string submissionPath,
        string effectLedgerPath,
        string? resultCommit)
    {
        return new RunToReviewSubmissionAttempt(
            true,
            true,
            false,
            submission,
            submissionPath,
            effectLedgerPath,
            null,
            resultCommit,
            null,
            null,
            null);
    }

    public static RunToReviewSubmissionAttempt Commit(
        RunToReviewSubmissionRecord submission,
        string submissionPath,
        string effectLedgerPath,
        string effectLedgerEventHash,
        string? resultCommit,
        string? stateTransitionCertificatePath,
        string? stateTransitionCertificateHash)
    {
        return new RunToReviewSubmissionAttempt(
            true,
            true,
            true,
            submission,
            submissionPath,
            effectLedgerPath,
            effectLedgerEventHash,
            resultCommit,
            stateTransitionCertificatePath,
            stateTransitionCertificateHash,
            null);
    }

    public static RunToReviewSubmissionAttempt Block(string failureMessage)
    {
        return new RunToReviewSubmissionAttempt(false, false, false, null, null, null, null, null, null, null, failureMessage);
    }
}

public sealed record RunToReviewSubmissionRecord
{
    public string Schema { get; init; } = "carves.run_to_review_submission.v0.98-rc.p4";

    public string SubmissionId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string RunId { get; init; } = string.Empty;

    public string WorkerRunId { get; init; } = string.Empty;

    public string TerminalState { get; init; } = "submitted_to_review";

    public string TaskStateOnSuccess { get; init; } = "REVIEW";

    public bool ReviewVerdictWritten { get; init; }

    public bool TaskCompleted { get; init; }

    public string? ResultCommit { get; init; }

    public string ResultCommitStatus { get; init; } = string.Empty;

    public string ResultCommitPolicy { get; init; } = string.Empty;

    public string? WorktreePath { get; init; }

    public string? EvidencePath { get; init; }

    public string? CommandLogPath { get; init; }

    public string? BuildLogPath { get; init; }

    public string? TestLogPath { get; init; }

    public string? PatchPath { get; init; }

    public string? BoundaryDecisionPath { get; init; }

    public string BoundaryWritebackDecision { get; init; } = string.Empty;

    public IReadOnlyList<string> BoundaryReasonCodes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredEvidence { get; init; } = Array.Empty<string>();

    public string EffectLedgerPath { get; init; } = string.Empty;

    public string StateTransitionCertificatePath { get; init; } = string.Empty;

    public string StateTransitionCertificateHash { get; init; } = string.Empty;

    public IReadOnlyList<string> CertifiedTransitions { get; init; } = Array.Empty<string>();

    public string ReceiptSummary { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record RunToReviewResultCommit(bool CanProceed, string? Commit, string Status, string? FailureMessage)
{
    public static RunToReviewResultCommit Allow(string? commit, string status)
    {
        return new RunToReviewResultCommit(true, commit, status, null);
    }

    public static RunToReviewResultCommit Block(string failureMessage)
    {
        return new RunToReviewResultCommit(false, null, "failed", failureMessage);
    }
}
