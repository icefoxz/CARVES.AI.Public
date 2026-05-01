using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildWorkerDispatchPilotEvidence(string taskId)
    {
        var task = services.TaskGraphService.GetTask(taskId);
        var runs = services.ExecutionRunService.ListRuns(taskId);
        var latestRun = runs.LastOrDefault();
        var workerArtifact = services.ArtifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var safetyArtifact = services.ArtifactRepository.TryLoadSafetyArtifact(taskId);
        var reviewArtifact = services.ArtifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var resultEnvelopePath = $".ai/execution/{taskId}/result.json";
        var resultEnvelope = TryLoadJson<ResultEnvelope>(resultEnvelopePath);
        var reviewSubmissionPath = ResolveMetadataPath(task.Metadata, "review_submission_sidecar_path", latestRun?.RunId, "review-submission.json");
        var effectLedgerPath = ResolveMetadataPath(task.Metadata, "review_submission_effect_ledger_path", latestRun?.RunId, "effect-ledger.jsonl");
        var stateTransitionCertificatePath = task.Metadata.GetValueOrDefault("review_submission_state_transition_certificate_path");
        var reviewArtifactPath = $".ai/artifacts/reviews/{taskId}.json";
        var reviewSubmissionPresent = FileExists(reviewSubmissionPath);
        var effectLedgerPresent = FileExists(effectLedgerPath);
        var stateTransitionCertificatePresent = FileExists(stateTransitionCertificatePath);
        var reviewArtifactPresent = FileExists(reviewArtifactPath) && reviewArtifact is not null;
        var closureDecision = reviewArtifact?.ClosureBundle.ClosureDecision;
        var reviewBundlePresent = reviewArtifact is not null
                                  && !string.IsNullOrWhiteSpace(reviewArtifact.ClosureBundle.SchemaVersion);
        var closureDecisionPresent = closureDecision is not null
                                     && !string.IsNullOrWhiteSpace(closureDecision.SchemaVersion);
        var resultMatchesKnownRun = resultEnvelope is not null
                                    && !string.IsNullOrWhiteSpace(resultEnvelope.ExecutionRunId)
                                    && runs.Any(run => string.Equals(run.RunId, resultEnvelope.ExecutionRunId, StringComparison.Ordinal));
        var hostValidationDecision = EvaluateWorkerDispatchHostValidation(
            taskId,
            resultEnvelope,
            workerArtifact,
            latestRun?.RunId);

        var missingLinks = BuildMissingPilotLinks(
            latestRun is not null,
            workerArtifact is not null,
            safetyArtifact is not null,
            resultEnvelope is not null,
            reviewSubmissionPresent,
            effectLedgerPresent,
            reviewArtifactPresent,
            reviewBundlePresent,
            closureDecisionPresent,
            hostValidationDecision.Valid);
        var chainComplete = missingLinks.Count == 0;

        return new JsonObject
        {
            ["kind"] = "worker_dispatch_pilot_evidence",
            ["task_id"] = taskId,
            ["task_status"] = task.Status.ToString(),
            ["pilot_chain_complete"] = chainComplete,
            ["readback_only"] = true,
            ["dispatch_projection_only"] = true,
            ["creates_task_queue"] = false,
            ["creates_execution_truth_root"] = false,
            ["writes_task_truth"] = false,
            ["truth_owner"] = ".ai/tasks/ plus Host-routed lifecycle artifacts",
            ["schedule_callback_readback"] = BuildScheduleCallbackReadback(
                taskId,
                latestRun?.RunId,
                resultEnvelope?.ExecutionRunId,
                resultMatchesKnownRun,
                hostValidationDecision,
                chainComplete,
                missingLinks),
            ["dispatch"] = new JsonObject
            {
                ["host_dispatch_observed"] = latestRun is not null,
                ["run_count"] = runs.Count,
                ["latest_run_id"] = latestRun?.RunId,
                ["latest_run_status"] = latestRun?.Status.ToString(),
                ["latest_run_trigger_reason"] = latestRun?.TriggerReason.ToString(),
                ["latest_run_result_envelope_path"] = latestRun?.ResultEnvelopePath,
                ["known_run_ids"] = ToJsonArray(runs.Select(run => run.RunId)),
            },
            ["worker_evidence"] = workerArtifact is null
                ? new JsonObject
                {
                    ["present"] = false,
                }
                : new JsonObject
                {
                    ["present"] = true,
                    ["worker_run_id"] = workerArtifact.Result.RunId,
                    ["worker_status"] = workerArtifact.Result.Status.ToString(),
                    ["backend_id"] = workerArtifact.Result.BackendId,
                    ["provider_id"] = workerArtifact.Result.ProviderId,
                    ["adapter_id"] = workerArtifact.Result.AdapterId,
                    ["summary"] = workerArtifact.Result.Summary,
                    ["evidence_path"] = workerArtifact.Evidence.EvidencePath,
                    ["evidence_completeness"] = workerArtifact.Evidence.EvidenceCompleteness.ToString(),
                    ["evidence_strength"] = workerArtifact.Evidence.EvidenceStrength.ToString(),
                    ["command_log_ref"] = workerArtifact.Evidence.CommandLogRef,
                    ["test_output_ref"] = workerArtifact.Evidence.TestOutputRef,
                    ["patch_ref"] = workerArtifact.Evidence.PatchRef,
                    ["changed_files"] = ToJsonArray(workerArtifact.Result.ChangedFiles
                        .Concat(workerArtifact.Result.ObservedChangedFiles)
                        .Concat(workerArtifact.Evidence.FilesWritten)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                },
            ["safety_gate"] = safetyArtifact is null
                ? new JsonObject
                {
                    ["safety_artifact_present"] = false,
                    ["safety_gate_status"] = "missing",
                    ["safety_gate_allowed"] = false,
                    ["safety_issues"] = new JsonArray(),
                }
                : new JsonObject
                {
                    ["safety_artifact_present"] = true,
                    ["safety_gate_status"] = FormatPilotSafetyStatus(safetyArtifact.Decision.Outcome),
                    ["safety_gate_allowed"] = safetyArtifact.Decision.Allowed,
                    ["safety_issues"] = ToJsonArray(safetyArtifact.Decision.Issues
                        .Select(issue => $"{issue.Code}: {issue.Message}")),
                },
            ["result_ingestion"] = new JsonObject
            {
                ["result_envelope_present"] = resultEnvelope is not null,
                ["result_envelope_path"] = resultEnvelopePath,
                ["result_status"] = resultEnvelope?.Status,
                ["result_execution_run_id"] = resultEnvelope?.ExecutionRunId,
                ["result_matches_known_host_run"] = resultMatchesKnownRun,
                ["review_submission_path"] = reviewSubmissionPath,
                ["review_submission_present"] = reviewSubmissionPresent,
                ["effect_ledger_path"] = effectLedgerPath,
                ["effect_ledger_present"] = effectLedgerPresent,
                ["state_transition_certificate_path"] = stateTransitionCertificatePath,
                ["state_transition_certificate_present"] = stateTransitionCertificatePresent,
                ["terminal_state"] = task.Metadata.GetValueOrDefault("review_submission_terminal_state"),
                ["result_commit_status"] = task.Metadata.GetValueOrDefault("review_submission_result_commit_status"),
            },
            ["host_validation"] = BuildWorkerDispatchHostValidation(
                taskId,
                latestRun?.RunId,
                resultEnvelope,
                workerArtifact,
                hostValidationDecision),
            ["review"] = new JsonObject
            {
                ["review_artifact_path"] = reviewArtifactPath,
                ["review_artifact_present"] = reviewArtifactPresent,
                ["review_bundle_present"] = reviewBundlePresent,
                ["closure_decision_present"] = closureDecisionPresent,
                ["resulting_status"] = reviewArtifact?.ResultingStatus.ToString(),
                ["decision_status"] = reviewArtifact?.DecisionStatus.ToString(),
                ["validation_passed"] = reviewArtifact?.ValidationPassed,
                ["closure_decision"] = closureDecision is null
                    ? null
                    : new JsonObject
                    {
                        ["status"] = closureDecision.Status,
                        ["decision"] = closureDecision.Decision,
                        ["writeback_allowed"] = closureDecision.WritebackAllowed,
                        ["result_source"] = closureDecision.ResultSource,
                        ["accepted_patch_source"] = closureDecision.AcceptedPatchSource,
                        ["worker_result_verdict"] = closureDecision.WorkerResultVerdict,
                        ["reviewer_decision"] = closureDecision.ReviewerDecision,
                        ["required_gate_status"] = closureDecision.RequiredGateStatus,
                        ["contract_matrix_status"] = closureDecision.ContractMatrixStatus,
                        ["safety_status"] = closureDecision.SafetyStatus,
                        ["blockers"] = ToJsonArray(closureDecision.Blockers),
                    },
            },
            ["completeness"] = new JsonObject
            {
                ["missing_links"] = ToJsonArray(missingLinks),
                ["next_action"] = chainComplete
                    ? "Pilot evidence chain is complete; continue with review decision or writeback gate as governed by the review bundle."
                    : $"Complete missing pilot evidence links: {string.Join(", ", missingLinks)}.",
            },
        };
    }

    private static JsonObject BuildScheduleCallbackReadback(
        string taskId,
        string? latestRunId,
        string? resultExecutionRunId,
        bool resultMatchesKnownRun,
        ResultValidityDecision hostValidationDecision,
        bool chainComplete,
        IReadOnlyList<string> missingLinks)
    {
        var observedRun = !string.IsNullOrWhiteSpace(latestRunId);
        return new JsonObject
        {
            ["kind"] = "worker_automation_schedule_callback_readback",
            ["schema_version"] = "worker-automation-schedule-callback-readback.v1",
            ["task_id"] = taskId,
            ["status"] = ResolveScheduleCallbackReadbackStatus(
                observedRun,
                resultMatchesKnownRun,
                hostValidationDecision,
                chainComplete),
            ["task_run_command"] = $"task run {taskId}",
            ["evidence_command"] = $"api worker-dispatch-pilot-evidence {taskId}",
            ["run_observed"] = observedRun,
            ["observed_execution_run_id"] = latestRunId,
            ["result_execution_run_id"] = resultExecutionRunId,
            ["result_matches_known_host_run"] = resultMatchesKnownRun,
            ["host_validation_status"] = FormatHostValidationStatus(hostValidationDecision),
            ["host_validation_valid"] = hostValidationDecision.Valid,
            ["host_validation_reason_code"] = hostValidationDecision.ReasonCode,
            ["host_validation_message"] = hostValidationDecision.Message,
            ["evidence_chain_complete"] = chainComplete,
            ["missing_links"] = ToJsonArray(missingLinks),
            ["callback_can_report_run_id"] = observedRun,
            ["callback_can_report_closure"] = chainComplete && hostValidationDecision.Valid,
            ["writes_truth"] = false,
            ["marks_task_completed"] = false,
            ["next_action"] = ResolveScheduleCallbackReadbackNextAction(
                taskId,
                observedRun,
                resultMatchesKnownRun,
                hostValidationDecision,
                chainComplete,
                missingLinks),
        };
    }

    private static string ResolveScheduleCallbackReadbackStatus(
        bool observedRun,
        bool resultMatchesKnownRun,
        ResultValidityDecision hostValidationDecision,
        bool chainComplete)
    {
        if (!observedRun)
        {
            return "waiting_for_task_run";
        }

        if (!resultMatchesKnownRun)
        {
            return "run_observed_result_mismatch";
        }

        if (!hostValidationDecision.Valid
            && !string.Equals(hostValidationDecision.ReasonCode, "result_envelope_missing", StringComparison.Ordinal))
        {
            return "host_validation_blocked";
        }

        return chainComplete
            ? "evidence_chain_complete"
            : "evidence_chain_incomplete";
    }

    private static string ResolveScheduleCallbackReadbackNextAction(
        string taskId,
        bool observedRun,
        bool resultMatchesKnownRun,
        ResultValidityDecision hostValidationDecision,
        bool chainComplete,
        IReadOnlyList<string> missingLinks)
    {
        if (!observedRun)
        {
            return $"Run `task run {taskId}` through Host, then rerun `api worker-dispatch-pilot-evidence {taskId}`.";
        }

        if (!resultMatchesKnownRun)
        {
            return "Inspect execution run metadata and result envelope before reporting callback completion.";
        }

        if (!hostValidationDecision.Valid)
        {
            return $"Resolve Host validation blocker `{hostValidationDecision.ReasonCode}` before reporting callback closure.";
        }

        if (chainComplete)
        {
            return "Report observed_execution_run_id and continue with governed review/writeback gates.";
        }

        return $"Report observed_execution_run_id, then complete missing evidence links: {string.Join(", ", missingLinks)}.";
    }

    private ResultValidityDecision EvaluateWorkerDispatchHostValidation(
        string taskId,
        ResultEnvelope? resultEnvelope,
        WorkerExecutionArtifact? workerArtifact,
        string? latestRunId)
    {
        if (resultEnvelope is null)
        {
            return new ResultValidityDecision(
                false,
                "result_envelope_missing",
                $"Host validation for '{taskId}' is waiting for a result envelope.");
        }

        return new ResultValidityPolicy(services.Paths).Evaluate(
            taskId,
            resultEnvelope,
            workerArtifact,
            latestRunId);
    }

    private static JsonObject BuildWorkerDispatchHostValidation(
        string taskId,
        string? latestRunId,
        ResultEnvelope? resultEnvelope,
        WorkerExecutionArtifact? workerArtifact,
        ResultValidityDecision decision)
    {
        var claim = workerArtifact?.Result.CompletionClaim;
        return new JsonObject
        {
            ["kind"] = "worker_dispatch_host_validation",
            ["schema_version"] = "worker-dispatch-host-validation.v1",
            ["task_id"] = taskId,
            ["status"] = FormatHostValidationStatus(decision),
            ["valid"] = decision.Valid,
            ["reason_code"] = decision.ReasonCode,
            ["message"] = decision.Message,
            ["execution_run_id"] = latestRunId,
            ["result_execution_run_id"] = resultEnvelope?.ExecutionRunId,
            ["worker_run_id"] = workerArtifact?.Result.RunId,
            ["evidence_path"] = decision.Evidence?.EvidencePath ?? workerArtifact?.Evidence.EvidencePath,
            ["writeback_allowed_by_host_validator"] = decision.Valid,
            ["worker_claim_is_truth"] = claim?.ClaimIsTruth ?? false,
            ["worker_claim_is_not_truth"] = !(claim?.ClaimIsTruth ?? false),
            ["worker_claim_host_validation_required"] = claim?.HostValidationRequired ?? true,
            ["completion_claim_required"] = claim?.Required ?? true,
            ["completion_claim_status"] = claim?.Status ?? "not_recorded",
            ["completion_claim_source"] = claim?.Source,
            ["completion_claim_packet_validation_status"] = claim?.PacketValidationStatus ?? "not_evaluated",
            ["completion_claim_packet_validation_blockers"] = ToJsonArray(claim?.PacketValidationBlockers ?? Array.Empty<string>()),
            ["blockers"] = decision.Valid
                ? new JsonArray()
                : new JsonArray(decision.ReasonCode),
            ["review_bundle_still_required"] = true,
            ["schedule_callback_may_report_validation"] = decision.Valid,
            ["writes_truth"] = false,
            ["marks_task_completed"] = false,
        };
    }

    private static string FormatHostValidationStatus(ResultValidityDecision decision)
    {
        if (decision.Valid)
        {
            return "valid";
        }

        return string.Equals(decision.ReasonCode, "result_envelope_missing", StringComparison.Ordinal)
            ? "not_ready"
            : "blocked";
    }

    private string? ResolveMetadataPath(
        IReadOnlyDictionary<string, string> metadata,
        string metadataKey,
        string? runId,
        string fileName)
    {
        if (metadata.TryGetValue(metadataKey, out var path) && !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return string.IsNullOrWhiteSpace(runId)
            ? null
            : $".ai/artifacts/worker-executions/{runId}/{fileName}";
    }

    private bool FileExists(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return false;
        }

        var path = relativeOrAbsolutePath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(services.Paths.RepoRoot, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar));
        }

        return File.Exists(path);
    }

    private static IReadOnlyList<string> BuildMissingPilotLinks(
        bool dispatchRunPresent,
        bool workerEvidencePresent,
        bool safetyArtifactPresent,
        bool resultEnvelopePresent,
        bool reviewSubmissionPresent,
        bool effectLedgerPresent,
        bool reviewArtifactPresent,
        bool reviewBundlePresent,
        bool closureDecisionPresent,
        bool hostValidationPassed)
    {
        var missing = new List<string>();
        AddIfMissing(missing, dispatchRunPresent, "host_dispatch_run");
        AddIfMissing(missing, workerEvidencePresent, "worker_evidence");
        AddIfMissing(missing, safetyArtifactPresent, "safety_artifact");
        AddIfMissing(missing, resultEnvelopePresent, "result_envelope");
        AddIfMissing(missing, reviewSubmissionPresent, "review_submission_sidecar");
        AddIfMissing(missing, effectLedgerPresent, "effect_ledger");
        AddIfMissing(missing, reviewArtifactPresent, "review_artifact");
        AddIfMissing(missing, reviewBundlePresent, "review_bundle");
        AddIfMissing(missing, closureDecisionPresent, "closure_decision");
        AddIfMissing(missing, hostValidationPassed, "host_validation");
        return missing;
    }

    private static void AddIfMissing(List<string> missing, bool present, string linkId)
    {
        if (!present)
        {
            missing.Add(linkId);
        }
    }

    private static string FormatPilotSafetyStatus(SafetyOutcome outcome)
    {
        return outcome switch
        {
            SafetyOutcome.Allow => "allow",
            SafetyOutcome.NeedsReview => "needs_review",
            SafetyOutcome.Blocked => "blocked",
            _ => outcome.ToString().ToLowerInvariant(),
        };
    }
}
