using System.Globalization;
using System.Text.Json;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private TaskNode ApplyResult(TaskNode task, ResultEnvelope envelope, string fingerprint)
    {
        var totalRuns = ParseCounter(task.Metadata, "execution_total_runs") + 1;
        var successCount = ParseCounter(task.Metadata, "execution_success_count");
        var failureStreak = ParseCounter(task.Metadata, "execution_failure_streak");
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["last_result_fingerprint"] = fingerprint,
            ["last_result_status"] = envelope.Status,
            ["last_result_stop_reason"] = envelope.Result.StopReason,
            ["execution_total_runs"] = totalRuns.ToString(CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath))
        {
            metadata["execution_evidence_path"] = envelope.ExecutionEvidencePath!;
        }

        if (!string.IsNullOrWhiteSpace(envelope.Failure.Type))
        {
            metadata["last_result_failure_type"] = envelope.Failure.Type!;
        }

        var status = envelope.Status.ToLowerInvariant() switch
        {
            "success" => DomainTaskStatus.Completed,
            "failed" => DomainTaskStatus.Failed,
            "blocked" => DomainTaskStatus.Blocked,
            _ => throw new InvalidOperationException($"Unsupported result status '{envelope.Status}'."),
        };

        if (status == DomainTaskStatus.Completed)
        {
            successCount += 1;
            failureStreak = 0;
        }
        else
        {
            failureStreak += 1;
        }

        metadata["execution_success_count"] = successCount.ToString(CultureInfo.InvariantCulture);
        metadata["execution_failure_streak"] = failureStreak.ToString(CultureInfo.InvariantCulture);

        var review = status switch
        {
            DomainTaskStatus.Completed => new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Result envelope marked the task successful.",
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = true,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
            DomainTaskStatus.Blocked => new PlannerReview
            {
                Verdict = PlannerVerdict.Blocked,
                Reason = envelope.Failure.Message ?? "Result envelope marked the task blocked.",
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
            _ => new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = envelope.Failure.Message ?? "Result envelope marked the task failed.",
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
        };

        return Clone(task, status, review, metadata);
    }

    private TaskNode ApplyReviewAdmission(TaskNode task, ResultEnvelope envelope, string fingerprint, BoundaryDecision decision)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["last_result_fingerprint"] = fingerprint,
            ["last_result_status"] = envelope.Status,
            ["last_result_stop_reason"] = envelope.Result.StopReason,
        };
        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath))
        {
            metadata["execution_evidence_path"] = envelope.ExecutionEvidencePath!;
        }

        return Clone(
            task,
            DomainTaskStatus.Review,
            new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = decision.Summary,
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = true,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
            metadata,
            AcceptanceContractStatusProjector.MoveToHumanReview(task.AcceptanceContract));
    }

    private TaskNode ApplyQuarantinedResult(TaskNode task, ResultEnvelope envelope, string fingerprint, BoundaryDecision decision)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["last_result_fingerprint"] = fingerprint,
            ["last_result_status"] = envelope.Status,
            ["last_result_stop_reason"] = envelope.Result.StopReason,
        };
        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath))
        {
            metadata["execution_evidence_path"] = envelope.ExecutionEvidencePath!;
        }

        return Clone(
            task,
            DomainTaskStatus.Blocked,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Blocked,
                Reason = decision.Summary,
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = false,
                ScopeDriftDetected = false,
            },
            metadata);
    }

    private TaskNode ApplyBoundaryMetadata(TaskNode task, ExecutionBoundaryAssessment assessment, ExecutionBoundaryArtifactSet artifacts, BoundaryDecision decision, PacketEnforcementRecord packetEnforcement)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["execution_budget_size"] = assessment.Budget.Size.ToString().ToLowerInvariant(),
            ["execution_budget_summary"] = assessment.Budget.Summary,
            ["execution_budget_confidence"] = assessment.Budget.ConfidenceLevel.ToString().ToLowerInvariant(),
            ["execution_risk_level"] = assessment.RiskLevel.ToString().ToLowerInvariant(),
            ["execution_risk_score"] = assessment.RiskScore.ToString(CultureInfo.InvariantCulture),
            ["execution_risk_reason"] = string.Join(" ", assessment.Reasons),
            ["execution_boundary_decision"] = assessment.Decision.ToString().ToLowerInvariant(),
            ["execution_boundary_budget_path"] = artifacts.BudgetPath,
            ["execution_boundary_telemetry_path"] = artifacts.TelemetryPath,
            ["execution_boundary_decision_path"] = artifacts.DecisionPath ?? executionBoundaryArtifactService.GetDecisionPath(task.TaskId),
            ["execution_boundary_writeback_decision"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(decision.WritebackDecision.ToString()),
            ["execution_boundary_decision_confidence"] = decision.DecisionConfidence.ToString(CultureInfo.InvariantCulture),
            ["execution_boundary_reason_codes"] = string.Join(",", decision.ReasonCodes),
            ["execution_boundary_summary"] = decision.Summary,
            ["execution_boundary_reviewer_required"] = decision.ReviewerRequired.ToString().ToLowerInvariant(),
            ["execution_boundary_evidence_status"] = decision.EvidenceStatus,
            ["execution_boundary_safety_status"] = decision.SafetyStatus,
            ["execution_boundary_test_status"] = decision.TestStatus,
            ["execution_failure_lane"] = decision.FailureLane,
            ["execution_failure_next_action"] = decision.RecommendedNextAction ?? string.Empty,
            ["execution_packet_id"] = packetEnforcement.PacketId,
            ["execution_packet_enforcement_path"] = NormalizeRepoRelative(packetEnforcementService.GetRecordPath(task.TaskId)),
            ["execution_packet_enforcement_verdict"] = packetEnforcement.Verdict,
            ["execution_packet_enforcement_summary"] = packetEnforcement.Summary,
            ["execution_packet_enforcement_reason_codes"] = string.Join(",", packetEnforcement.ReasonCodes),
            ["execution_packet_requested_action"] = packetEnforcement.RequestedAction,
            ["execution_packet_requested_action_class"] = packetEnforcement.RequestedActionClass,
            ["execution_packet_planner_only_action_attempted"] = packetEnforcement.PlannerOnlyActionAttempted.ToString().ToLowerInvariant(),
            ["execution_packet_lifecycle_writeback_attempted"] = packetEnforcement.LifecycleWritebackAttempted.ToString().ToLowerInvariant(),
            ["execution_packet_off_packet_edit_detected"] = (packetEnforcement.OffPacketFiles.Count > 0).ToString().ToLowerInvariant(),
            ["execution_packet_truth_write_detected"] = (packetEnforcement.TruthWriteFiles.Count > 0).ToString().ToLowerInvariant(),
            ["execution_packet_contract_valid"] = packetEnforcement.PacketContractValid.ToString().ToLowerInvariant(),
            ["managed_workspace_path_policy_status"] = assessment.ManagedWorkspacePathPolicy.Status,
            ["managed_workspace_path_policy_summary"] = assessment.ManagedWorkspacePathPolicy.Summary,
            ["managed_workspace_path_policy_next_action"] = assessment.ManagedWorkspacePathPolicy.RecommendedNextAction,
            ["managed_workspace_path_policy_scope_escape_count"] = assessment.ManagedWorkspacePathPolicy.ScopeEscapeCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_host_only_count"] = assessment.ManagedWorkspacePathPolicy.HostOnlyCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_deny_count"] = assessment.ManagedWorkspacePathPolicy.DenyCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_review_required_count"] = assessment.ManagedWorkspacePathPolicy.ReviewRequiredCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_enforced"] = assessment.ManagedWorkspacePathPolicy.EnforcementActive.ToString().ToLowerInvariant(),
        };

        if (!string.IsNullOrWhiteSpace(assessment.ManagedWorkspacePathPolicy.LeaseId))
        {
            metadata["managed_workspace_path_policy_lease_id"] = assessment.ManagedWorkspacePathPolicy.LeaseId!;
        }

        return Clone(task, task.Status, task.PlannerReview, metadata);
    }

    private static TaskNode ApplyRunToReviewSubmissionMetadata(TaskNode task, RunToReviewSubmissionAttempt submissionAttempt)
    {
        if (!submissionAttempt.Created || submissionAttempt.Submission is null)
        {
            return task;
        }

        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["review_submission_sidecar_path"] = submissionAttempt.SubmissionPath ?? string.Empty,
            ["review_submission_effect_ledger_path"] = submissionAttempt.EffectLedgerPath ?? string.Empty,
            ["review_submission_effect_ledger_event_hash"] = submissionAttempt.EffectLedgerEventHash ?? string.Empty,
            ["review_submission_result_commit_status"] = submissionAttempt.Submission.ResultCommitStatus,
            ["review_submission_terminal_state"] = submissionAttempt.Submission.TerminalState,
            ["review_submission_state_transition_certificate_path"] = submissionAttempt.StateTransitionCertificatePath ?? string.Empty,
            ["review_submission_state_transition_certificate_hash"] = submissionAttempt.StateTransitionCertificateHash ?? string.Empty,
            ["review_submission_certified_transitions"] = string.Join(",", submissionAttempt.Submission.CertifiedTransitions),
            ["review_submission_receipt"] = submissionAttempt.Submission.ReceiptSummary,
        };
        if (!string.IsNullOrWhiteSpace(submissionAttempt.ResultCommit))
        {
            metadata["review_submission_result_commit"] = submissionAttempt.ResultCommit!;
        }

        var updated = Clone(task, task.Status, task.PlannerReview, metadata, task.AcceptanceContract);
        if (!string.IsNullOrWhiteSpace(submissionAttempt.ResultCommit))
        {
            updated.SetResultCommit(submissionAttempt.ResultCommit);
        }

        return updated;
    }

    private TaskNode ApplyBoundaryStop(
        TaskNode task,
        ResultEnvelope envelope,
        string fingerprint,
        ExecutionBoundaryAssessment assessment,
        ExecutionBoundaryArtifactSet artifacts,
        ExecutionBoundaryViolation violation,
        ExecutionBoundaryReplanRequest replan,
        BoundaryDecision decision,
        PacketEnforcementRecord packetEnforcement)
    {
        var totalRuns = ParseCounter(task.Metadata, "execution_total_runs") + 1;
        var successCount = ParseCounter(task.Metadata, "execution_success_count");
        var failureStreak = ParseCounter(task.Metadata, "execution_failure_streak") + 1;
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["last_result_fingerprint"] = fingerprint,
            ["last_result_status"] = envelope.Status,
            ["last_result_stop_reason"] = envelope.Result.StopReason,
            ["execution_total_runs"] = totalRuns.ToString(CultureInfo.InvariantCulture),
            ["execution_success_count"] = successCount.ToString(CultureInfo.InvariantCulture),
            ["execution_failure_streak"] = failureStreak.ToString(CultureInfo.InvariantCulture),
            ["execution_budget_size"] = assessment.Budget.Size.ToString().ToLowerInvariant(),
            ["execution_budget_summary"] = assessment.Budget.Summary,
            ["execution_budget_confidence"] = assessment.Budget.ConfidenceLevel.ToString().ToLowerInvariant(),
            ["execution_risk_level"] = assessment.RiskLevel.ToString().ToLowerInvariant(),
            ["execution_risk_score"] = assessment.RiskScore.ToString(CultureInfo.InvariantCulture),
            ["execution_risk_reason"] = string.Join(" ", assessment.Reasons),
            ["execution_boundary_decision"] = ExecutionBoundaryDecision.Stop.ToString().ToLowerInvariant(),
            ["execution_boundary_budget_path"] = artifacts.BudgetPath,
            ["execution_boundary_telemetry_path"] = artifacts.TelemetryPath,
            ["execution_boundary_decision_path"] = artifacts.DecisionPath ?? executionBoundaryArtifactService.GetDecisionPath(task.TaskId),
            ["execution_boundary_writeback_decision"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(decision.WritebackDecision.ToString()),
            ["execution_boundary_decision_confidence"] = decision.DecisionConfidence.ToString(CultureInfo.InvariantCulture),
            ["execution_boundary_reason_codes"] = string.Join(",", decision.ReasonCodes),
            ["execution_boundary_summary"] = decision.Summary,
            ["execution_boundary_reviewer_required"] = decision.ReviewerRequired.ToString().ToLowerInvariant(),
            ["execution_boundary_evidence_status"] = decision.EvidenceStatus,
            ["execution_boundary_safety_status"] = decision.SafetyStatus,
            ["execution_boundary_test_status"] = decision.TestStatus,
            ["execution_failure_lane"] = decision.FailureLane,
            ["execution_failure_next_action"] = decision.RecommendedNextAction ?? string.Empty,
            ["execution_packet_id"] = packetEnforcement.PacketId,
            ["execution_packet_enforcement_path"] = NormalizeRepoRelative(packetEnforcementService.GetRecordPath(task.TaskId)),
            ["execution_packet_enforcement_verdict"] = packetEnforcement.Verdict,
            ["execution_packet_enforcement_summary"] = packetEnforcement.Summary,
            ["execution_packet_enforcement_reason_codes"] = string.Join(",", packetEnforcement.ReasonCodes),
            ["execution_packet_requested_action"] = packetEnforcement.RequestedAction,
            ["execution_packet_requested_action_class"] = packetEnforcement.RequestedActionClass,
            ["execution_packet_planner_only_action_attempted"] = packetEnforcement.PlannerOnlyActionAttempted.ToString().ToLowerInvariant(),
            ["execution_packet_lifecycle_writeback_attempted"] = packetEnforcement.LifecycleWritebackAttempted.ToString().ToLowerInvariant(),
            ["execution_packet_off_packet_edit_detected"] = (packetEnforcement.OffPacketFiles.Count > 0).ToString().ToLowerInvariant(),
            ["execution_packet_truth_write_detected"] = (packetEnforcement.TruthWriteFiles.Count > 0).ToString().ToLowerInvariant(),
            ["execution_packet_contract_valid"] = packetEnforcement.PacketContractValid.ToString().ToLowerInvariant(),
            ["managed_workspace_path_policy_status"] = assessment.ManagedWorkspacePathPolicy.Status,
            ["managed_workspace_path_policy_summary"] = assessment.ManagedWorkspacePathPolicy.Summary,
            ["managed_workspace_path_policy_next_action"] = assessment.ManagedWorkspacePathPolicy.RecommendedNextAction,
            ["managed_workspace_path_policy_scope_escape_count"] = assessment.ManagedWorkspacePathPolicy.ScopeEscapeCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_host_only_count"] = assessment.ManagedWorkspacePathPolicy.HostOnlyCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_deny_count"] = assessment.ManagedWorkspacePathPolicy.DenyCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_review_required_count"] = assessment.ManagedWorkspacePathPolicy.ReviewRequiredCount.ToString(CultureInfo.InvariantCulture),
            ["managed_workspace_path_policy_enforced"] = assessment.ManagedWorkspacePathPolicy.EnforcementActive.ToString().ToLowerInvariant(),
            ["boundary_stopped"] = "true",
            ["boundary_reason"] = violation.Reason.ToString(),
            ["boundary_detail"] = violation.Detail,
            ["boundary_violation_path"] = artifacts.ViolationPath ?? executionBoundaryArtifactService.GetViolationPath(task.TaskId),
            ["boundary_replan_path"] = artifacts.ReplanPath ?? executionBoundaryArtifactService.GetReplanPath(task.TaskId),
            ["boundary_replan_strategy"] = replan.Strategy.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(assessment.ManagedWorkspacePathPolicy.LeaseId))
        {
            metadata["managed_workspace_path_policy_lease_id"] = assessment.ManagedWorkspacePathPolicy.LeaseId!;
        }

        return Clone(
            task,
            DomainTaskStatus.Review,
            new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = string.IsNullOrWhiteSpace(violation.Detail)
                    ? $"Execution stopped by boundary gate: {violation.Reason}."
                    : $"Execution stopped by boundary gate: {violation.Detail}",
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = false,
                BoundaryPreserved = false,
                ScopeDriftDetected = violation.Reason is ExecutionBoundaryStopReason.ScopeViolation
                    or ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath
                    or ExecutionBoundaryStopReason.ManagedWorkspaceDeniedPath,
                FollowUpSuggestions = replan.FollowUpSuggestions,
            },
            metadata,
            AcceptanceContractStatusProjector.MoveToHumanReview(task.AcceptanceContract));
    }
}
