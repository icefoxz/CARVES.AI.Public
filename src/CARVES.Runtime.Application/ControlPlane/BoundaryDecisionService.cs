using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class BoundaryDecisionService
{
    public BoundaryDecision Evaluate(
        TaskNode task,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        SafetyArtifact? safetyArtifact,
        ResultValidityDecision validity,
        ExecutionBoundaryAssessment assessment,
        PacketEnforcementRecord? packetEnforcement = null)
    {
        var reasonCodes = new List<string>();
        var failureClassification = workerArtifact is null ? null : WorkerFailureSemantics.Classify(workerArtifact.Result);
        if (!validity.Valid)
        {
            reasonCodes.Add(validity.ReasonCode);
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.RejectResult,
                reviewerRequired: false,
                decisionConfidence: 1.0,
                reasonCodes,
                validity.Message,
                safetyStatus: ResolveSafetyStatus(safetyArtifact),
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: failureClassification?.NextAction);
        }

        if (packetEnforcement is not null)
        {
            if (string.Equals(packetEnforcement.Verdict, "reject", StringComparison.OrdinalIgnoreCase))
            {
                reasonCodes.AddRange(packetEnforcement.ReasonCodes);
                return Build(
                    task,
                    envelope,
                    validity,
                    BoundaryWritebackDecision.RejectResult,
                    reviewerRequired: true,
                    decisionConfidence: 1.0,
                    reasonCodes,
                    packetEnforcement.Summary,
                    safetyStatus: "pending_packet_enforcement",
                    failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                    recommendedNextAction: $"inspect packet-enforcement {task.TaskId} before retry or writeback");
            }

            if (string.Equals(packetEnforcement.Verdict, "quarantine", StringComparison.OrdinalIgnoreCase))
            {
                reasonCodes.AddRange(packetEnforcement.ReasonCodes);
                return Build(
                    task,
                    envelope,
                    validity,
                    BoundaryWritebackDecision.QuarantineResult,
                    reviewerRequired: true,
                    decisionConfidence: 1.0,
                    reasonCodes,
                    packetEnforcement.Summary,
                    safetyStatus: "pending_packet_enforcement",
                    failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                    recommendedNextAction: $"inspect packet-enforcement {task.TaskId} before retry or writeback");
            }
        }

        var safetyStatus = ResolveSafetyStatus(safetyArtifact);
        if (safetyArtifact is null)
        {
            reasonCodes.Add("safety_status_missing");
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.QuarantineResult,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                "Execution result cannot be admitted because upstream safety truth is missing.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: "inspect safety artifact persistence before review or writeback");
        }

        if (safetyArtifact.Decision.Outcome == SafetyOutcome.Blocked)
        {
            reasonCodes.Add("safety_blocked");
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.RejectResult,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                "Execution result was rejected because upstream safety blocked the run.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: "review safety violations before retry or writeback");
        }

        if (safetyArtifact.Decision.Outcome == SafetyOutcome.NeedsReview)
        {
            reasonCodes.Add("safety_review_required");
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.RequireHumanReview,
                reviewerRequired: true,
                decisionConfidence: 0.8,
                reasonCodes,
                "Execution result is admissible but upstream safety requires human review.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: "review the safety artifact before accepting the result");
        }

        if (assessment.ShouldStop)
        {
            reasonCodes.Add("boundary_stop");
            if (assessment.StopReason is not null)
            {
                reasonCodes.Add(assessment.StopReason.Value.ToString().ToLowerInvariant());
            }

            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.QuarantineResult,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                $"Execution stopped by boundary gate: {assessment.StopReason}.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: failureClassification?.NextAction);
        }

        if (assessment.Decision == ExecutionBoundaryDecision.Block)
        {
            reasonCodes.Add("boundary_block");
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.QuarantineResult,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                "Execution risk remained too high for result writeback.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "unknown",
                recommendedNextAction: failureClassification?.NextAction);
        }

        if (failureClassification?.Lane == WorkerFailureLane.Substrate)
        {
            reasonCodes.Add("substrate_failure");
            reasonCodes.Add(failureClassification.ReasonCode);
            if (!string.IsNullOrWhiteSpace(failureClassification.SubstrateCategory))
            {
                reasonCodes.Add(failureClassification.SubstrateCategory);
            }

            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.RetryableInfraFailure,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                envelope.Failure.Message ?? "Execution result was blocked before writeback.",
                safetyStatus: safetyStatus,
                failureLane: "substrate",
                recommendedNextAction: failureClassification.NextAction);
        }

        if (string.Equals(envelope.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            reasonCodes.Add("semantic_failure");
            if (string.Equals(envelope.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase))
            {
                reasonCodes.Add("build_failed");
            }

            if (string.Equals(envelope.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase))
            {
                reasonCodes.Add("tests_failed");
            }

            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.SemanticFailure,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                envelope.Failure.Message ?? "Execution failed semantic validation before writeback.",
                safetyStatus: safetyStatus,
                failureLane: "semantic",
                recommendedNextAction: failureClassification?.NextAction ?? "review semantic failure before retry or replan");
        }

        if (task.RequiresReviewBoundary || assessment.Budget.RequiresReviewBoundary || assessment.Decision == ExecutionBoundaryDecision.Review)
        {
            reasonCodes.Add("review_boundary");
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.AdmitToReview,
                reviewerRequired: true,
                decisionConfidence: 0.8,
                reasonCodes,
                "Execution result is valid but must stop at the review boundary.",
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "none",
                recommendedNextAction: failureClassification?.NextAction);
        }

        var directWritebackAcceptance = EvaluateDirectWritebackAcceptance(task, envelope, workerArtifact);
        if (!directWritebackAcceptance.Allowed)
        {
            reasonCodes.AddRange(directWritebackAcceptance.ReasonCodes);
            return Build(
                task,
                envelope,
                validity,
                BoundaryWritebackDecision.AdmitToReview,
                reviewerRequired: true,
                decisionConfidence: 1.0,
                reasonCodes,
                directWritebackAcceptance.Summary,
                safetyStatus: safetyStatus,
                failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "none",
                recommendedNextAction: "submit result to review because direct completion is not authorized by the acceptance contract");
        }

        reasonCodes.Add("writeback_allowed");
        return Build(
            task,
            envelope,
            validity,
            BoundaryWritebackDecision.AdmitToWriteback,
            reviewerRequired: false,
            decisionConfidence: 1.0,
            reasonCodes,
            "Execution result is valid for direct task writeback.",
            safetyStatus: safetyStatus,
            failureLane: failureClassification?.Lane.ToString().ToLowerInvariant() ?? "none",
            recommendedNextAction: failureClassification?.NextAction);
    }

    private static DirectWritebackAcceptanceDecision EvaluateDirectWritebackAcceptance(
        TaskNode task,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact)
    {
        var contract = task.AcceptanceContract;
        if (contract is null)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_contract_missing"],
                "Direct completed writeback requires an acceptance contract that explicitly permits auto-complete.");
        }

        if (contract.Status is not (AcceptanceContractLifecycleStatus.Compiled
            or AcceptanceContractLifecycleStatus.Green
            or AcceptanceContractLifecycleStatus.Accepted
            or AcceptanceContractLifecycleStatus.ProvisionalAccepted))
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_contract_not_ready"],
                $"Direct completed writeback is not allowed while acceptance contract '{contract.ContractId}' is {contract.Status}.");
        }

        if (!contract.AutoCompleteAllowed)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_auto_complete_not_allowed"],
                $"Direct completed writeback is not allowed because acceptance contract '{contract.ContractId}' does not explicitly permit auto-complete.");
        }

        if (contract.HumanReview.Required)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_human_review_required"],
                $"Direct completed writeback is not allowed because acceptance contract '{contract.ContractId}' requires human review.");
        }

        if (contract.EvidenceRequired.Count == 0)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_evidence_contract_empty"],
                $"Direct completed writeback is not allowed because acceptance contract '{contract.ContractId}' does not require any pre-writeback evidence.");
        }

        var postWritebackOnly = contract.EvidenceRequired
            .Where(requirement => IsPostWritebackOnlyEvidenceRequirement(requirement.Type))
            .Select(requirement => string.IsNullOrWhiteSpace(requirement.Description)
                ? requirement.Type
                : $"{requirement.Type} ({requirement.Description})")
            .ToArray();
        if (postWritebackOnly.Length != 0)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_evidence_post_writeback_only"],
                $"Direct completed writeback is not allowed because acceptance contract '{contract.ContractId}' requires post-writeback evidence: {string.Join("; ", postWritebackOnly)}.");
        }

        var missing = contract.EvidenceRequired
            .Where(requirement => !IsDirectWritebackEvidenceSatisfied(requirement, envelope, workerArtifact))
            .Select(requirement => string.IsNullOrWhiteSpace(requirement.Description)
                ? requirement.Type
                : $"{requirement.Type} ({requirement.Description})")
            .ToArray();
        if (missing.Length != 0)
        {
            return DirectWritebackAcceptanceDecision.Block(
                ["acceptance_evidence_missing"],
                $"Direct completed writeback is not allowed because acceptance evidence is missing: {string.Join("; ", missing)}.");
        }

        return DirectWritebackAcceptanceDecision.Allow;
    }

    private static bool IsDirectWritebackEvidenceSatisfied(
        AcceptanceContractEvidenceRequirement requirement,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact)
    {
        var evidence = workerArtifact?.Evidence;
        return NormalizeRequirement(requirement.Type) switch
        {
            "validation" or "validation_passed" => string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(envelope.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(envelope.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase),
            "validation_evidence" => evidence is not null
                && (!string.IsNullOrWhiteSpace(evidence.EvidencePath) || evidence.CommandsExecuted.Count > 0),
            "patch" => evidence is not null
                && (!string.IsNullOrWhiteSpace(evidence.PatchRef)
                    || !string.IsNullOrWhiteSpace(evidence.PatchHash)
                    || evidence.FilesWritten.Count > 0),
            "command_log" or "command_trace" => evidence is not null
                && (!string.IsNullOrWhiteSpace(evidence.CommandLogRef) || evidence.CommandsExecuted.Count > 0),
            "build_output" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.BuildOutputRef),
            "test_output" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.TestOutputRef),
            "files_written" or "changed_files" => evidence is not null && evidence.FilesWritten.Count > 0,
            "worktree" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.WorktreePath),
            _ => false,
        };
    }

    private static bool IsPostWritebackOnlyEvidenceRequirement(string? requirementType)
    {
        return NormalizeRequirement(requirementType) switch
        {
            "writeback" or "result_commit" or "review_verdict" or "review_writeback" or "merge" or "release" => true,
            _ => false,
        };
    }

    private static string NormalizeRequirement(string? requirementType)
    {
        return (requirementType ?? string.Empty)
            .Trim()
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToLowerInvariant();
    }

    private static BoundaryDecision Build(
        TaskNode task,
        ResultEnvelope envelope,
        ResultValidityDecision validity,
        BoundaryWritebackDecision decision,
        bool reviewerRequired,
        double decisionConfidence,
        IReadOnlyList<string> reasonCodes,
        string summary,
        string safetyStatus,
        string failureLane,
        string? recommendedNextAction)
    {
        return new BoundaryDecision
        {
            TaskId = task.TaskId,
            RunId = envelope.ExecutionRunId,
            EvidenceStatus = ResolveEvidenceStatus(validity)
                ?? (validity.Valid ? "partial" : "missing"),
            SafetyStatus = safetyStatus,
            TestStatus = envelope.Validation.Tests.ToLowerInvariant(),
            FailureLane = failureLane,
            WritebackDecision = decision,
            ReasonCodes = reasonCodes,
            ReviewerRequired = reviewerRequired,
            DecisionConfidence = decisionConfidence,
            Summary = summary,
            RecommendedNextAction = recommendedNextAction,
        };
    }

    private static string ResolveSafetyStatus(SafetyArtifact? safetyArtifact)
    {
        return safetyArtifact?.Decision.Outcome switch
        {
            SafetyOutcome.Allow => "allow",
            SafetyOutcome.NeedsReview => "needs_review",
            SafetyOutcome.Blocked => "blocked",
            _ => "missing",
        };
    }

    private static string? ResolveEvidenceStatus(ResultValidityDecision validity)
    {
        if (validity.Evidence is null)
        {
            return null;
        }

        var strength = validity.Evidence.EvidenceStrength;
        if (strength == ExecutionEvidenceStrength.Missing && validity.Evidence.EvidenceCompleteness == ExecutionEvidenceCompleteness.Complete)
        {
            strength = ExecutionEvidenceStrength.Verifiable;
        }

        return strength.ToString().ToLowerInvariant();
    }
}

internal sealed record DirectWritebackAcceptanceDecision(
    bool Allowed,
    IReadOnlyList<string> ReasonCodes,
    string Summary)
{
    public static DirectWritebackAcceptanceDecision Allow { get; } = new(true, [], "Acceptance contract permits direct completed writeback.");

    public static DirectWritebackAcceptanceDecision Block(IReadOnlyList<string> reasonCodes, string summary)
    {
        return new DirectWritebackAcceptanceDecision(false, reasonCodes, summary);
    }
}
